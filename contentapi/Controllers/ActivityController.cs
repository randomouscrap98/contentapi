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
    }

    public class ActivityResult
    {
        public List<ActivityView> activity {get;set;}
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

        protected IQueryable<EntityRRGroup> BasicPermissionQuery(IQueryable<EntityRelation> query, long user, string action)
        {
            var result = query.Join(services.provider.GetQueryable<EntityRelation>(), r => -r.entityId2, r2 => r2.entityId2,
                      (r,r2) => new EntityRRGroup() { relation = r2, relation2 = r });

                //NOTE: the relations are SWAPPED because the intial group we applied the search to is the COMMENTS,
                //but permission where expects the FIRST relation to be permissions
            
            //This means you can only read comments if you can read the content. Meaning you may be unable to read your own comment.... oh well.
            //result = PermissionWhere(result, user, action);
            result = result.Where(x => x.relation2.entityId1 <= 0 || (user > 0 && x.relation.type == keys.CreatorRelation && x.relation.entityId1 == user) ||
                (x.relation.type == action && (x.relation.entityId1 == 0 || x.relation.entityId1 == user)));

            return result;
        }

        protected EntityRelationSearch ModifySearch(EntityRelationSearch search)
        {
            search = LimitSearch(search);
            search.TypeLike = $"{keys.ActivityKey}";
            return search;
        }

        protected ActivityView ConvertToView(EntityRelation relation)
        {
            var view = new ActivityView(); //services.mapper.Map<ActivityView>(relation);

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
        public async Task<ActionResult<ActivityResult>> GetActivityAsync([FromQuery]ActivitySearch search)
        {
            var relationSearch = ModifySearch(services.mapper.Map<EntityRelationSearch>(search));

            if(string.IsNullOrWhiteSpace(search.Type))
                search.Type = "%";

            relationSearch.TypeLike += search.Type;

            var user = GetRequesterUidNoFail();

            var baseRelations = services.provider.ApplyEntityRelationSearch(services.provider.GetQueryable<EntityRelation>(), relationSearch, false);
            baseRelations = baseRelations.Where(x => x.type != $"{keys.ActivityKey}{keys.FileType}");

            var query = BasicPermissionQuery(baseRelations, user, keys.ReadAction);

            var idHusk =
                from x in query 
                group x by x.relation2.id into g
                select new EntityBase() { id = g.Key };

            var relations = await services.provider.GetListAsync(FinalizeHusk<EntityRelation>(idHusk, relationSearch));

            return new ActivityResult()
            {
                activity = relations.Select(x => ConvertToView(x)).ToList()
            };
        }
    }
}