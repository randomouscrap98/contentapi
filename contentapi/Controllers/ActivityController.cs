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

        //protected IQueryable<EntityRRGroup> BasicPermissionQuery(IQueryable<EntityRelation> query, long user, string action, bool includeAnonymous)
        //{
        //    var result = query.Join(services.provider.GetQueryable<EntityRelation>(), r => -r.entityId2, r2 => r2.entityId2,
        //              (r,r2) => new EntityRRGroup() { relation = r2, relation2 = r });

        //        //NOTE: the relations are SWAPPED because the intial group we applied the search to is the COMMENTS,
        //        //but permission where expects the FIRST relation to be permissions
        //    
        //    //result = PermissionWhere(result, user, action, (x) => x.relation2.entityId1 <= 0);

        //    result = PermissionWhere(result, user, action, new PermissionExtras() { allowNegativeOwnerRelation = includeAnonymous });
        //    //relation2 is OUR relations (the activity)
        //    //result = result.Where(x => (includeAnonymous && x.relation2.entityId1 <= 0) || (user > 0 && x.relation.type == keys.CreatorRelation && x.relation.entityId1 == user) ||
        //    //    (x.relation.type == action && (x.relation.entityId1 == 0 || x.relation.entityId1 == user)));

        //    return result;
        //}

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

            var idHusk = ConvertToHusk(query, x => x.relation.id);

            var relations = await services.provider.GetListAsync(FinalizeHusk<EntityRelation>(idHusk, relationSearch));

            return new ActivityResultView()
            {
                activity = relations.Select(x => ConvertToView(x)).ToList()
            };
        }
    }
}