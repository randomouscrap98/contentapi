using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class ActivitySearch : EntitySearchBase
    {
        public List<long> UserIds {get;set;} = new List<long>();
        public List<long> ContentIds {get;set;} = new List<long>();

        public string Type {get;set;}
        public bool IncludeAnonymous {get;set;}
        public TimeSpan recentCommentTime {get;set;}
    }

    public class ActivityControllerProfile : Profile
    {
        public ActivityControllerProfile() 
        {
            CreateMap<ActivitySearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.UserIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.ContentIds.Select(x => -x).ToList()));
            CreateMap<EntityRelation, ActivityView>()
                .ForMember(x => x.date, o => o.MapFrom(s => s.createDate))
                .ForMember(x => x.userId, o => o.MapFrom(s => s.entityId1))
                .ForMember(x => x.contentId, o => o.MapFrom(s => -s.entityId2))
                ; //Don't need to reverse this one
        }
    }

    public class ActivityController : BaseSimpleController
    {
        public ActivityController(ControllerServices services, ILogger<BaseSimpleController> logger) : base(services, logger)
        {
        }

        protected EntityRelationSearch ModifySearch(EntityRelationSearch search)
        {
            //It is safe to just call any endpoint, because the count is limited to 1000.
            search = LimitSearch(search);
            search.TypeLike = $"{keys.ActivityKey}";
            return search;
        }

        protected ActivityView ConvertToView(EntityRelation relation)
        {
            var view = new ActivityView();

            view.id = relation.id;
            view.date = (DateTime)relation.createDateProper();
            view.userId = relation.entityId1;
            view.contentId = -relation.entityId2;
            view.contentType = relation.type.Substring(keys.ActivityKey.Length + keys.ContentType.Length);
            view.action = relation.value.Substring(1, 1); //Assume it's 1 character
            view.extra = relation.value.Substring(keys.CreateAction.Length);

            return view;
        }

        [HttpGet]
        public async Task<ActionResult<ActivityResultView>> GetActivityAsync([FromQuery]ActivitySearch search)
        {
            var relationSearch = ModifySearch(services.mapper.Map<EntityRelationSearch>(search));

            if(string.IsNullOrWhiteSpace(search.Type))
                search.Type = "%";

            relationSearch.TypeLike += search.Type;

            var user = GetRequesterUidNoFail();

            var query = BasicReadQuery(user, relationSearch, x => -x.entityId2, new PermissionExtras() { allowNegativeOwnerRelation = search.IncludeAnonymous} )
                            .Where(x => x.relation.type != $"{keys.ActivityKey}{keys.FileType}");

            var relations = await services.provider.GetListAsync(FinalizeQuery<EntityRelation>(query, x=> x.relation.id, relationSearch));
            var result = new ActivityResultView() { activity = relations.Select(x => ConvertToView(x)).ToList(), };

            //No matter the search, get comments for up to the recent thing.
            if(search.recentCommentTime.Ticks > 0)
            {
                var commentSearch = new EntityRelationSearch()
                {
                    TypeLike = $"{keys.CommentHack}%",
                    CreateStart = DateTime.Now.Subtract(search.recentCommentTime),
                    Reverse = true
                };

                var commentQuery = BasicReadQuery(user, commentSearch, x => x.entityId1); //entityid1 is the parent content, they need perms
                var finalComments = await provider.GetListAsync(
                    FinalizeQuery<EntityRelation>(commentQuery, x => x.relation.id, commentSearch) //ALWAYS GIVE ID GOSH
                    .Select(x => new { contentId = x.entityId1, userId = -x.entityId2, date = x.createDate})); //We only want SOME fields, don't pull them all! TOO MUCH

                foreach(var group in finalComments.ToLookup(x => x.contentId))
                {
                    result.comments.Add(new CommentActivityView()
                    {
                        count = group.Count(),
                        parentId = group.Key,
                        userIds = group.Select(x => x.userId).Distinct().ToList(),
                        lastDate = group.Max(x => (DateTime)x.date),
                    });
                }
            }

            return result;
        }
    }
}