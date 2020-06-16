using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ActivitySearch : BaseSearch
    {
        public List<long> UserIds {get;set;} = new List<long>();
        public List<long> ContentIds {get;set;} = new List<long>();

        public IdLimiter ContentLimit {get;set;} = new IdLimiter();
        public string Type {get;set;}
        public bool IncludeAnonymous {get;set;} //This is queried in the SERVICE, eventually move it to HERE! 
    }

    public class ActivityViewSourceProfile : Profile
    {
        public ActivityViewSourceProfile() 
        {
            CreateMap<ActivitySearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.UserIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.ContentIds.Select(x => -x).ToList()));
        }
    }

    public class ActivityViewSource : BaseRelationViewSource<ActivityView, EntityRelation, EntityGroup, ActivitySearch>
    {
        public ActivityViewSource(ILogger<ActivityViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override string EntityType => Keys.ActivityKey;
        public override Expression<Func<EntityRelation, long>> PermIdSelector => x => -x.entityId2;

        public int TypeLength => Keys.ContentType.Length;

        /// <summary>
        /// Produce an activity for the given entity and action. Can include ONE piece of extra data.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="action"></param>
        /// <param name="extra"></param>
        /// <returns></returns>
        public EntityRelation MakeActivity(Entity entity, long user, string action, string extra = null)
        {
            var trimmedType = entity.type.TrimStart(Keys.HistoryKey.ToCharArray()); //.Substring(0, TypeLength);
            return FromView(new ActivityView()
            {
                id = 0,
                userId = user,
                contentId = entity.id,
                action = action,
                extra = extra,
                type = trimmedType.Substring(0, TypeLength), //Assume all types are same length!
                contentType = trimmedType.Substring(TypeLength),
                date = DateTime.Now
            });
        }

        public override ActivityView ToView(EntityRelation relation)
        {
            var view = new ActivityView();

            view.id = relation.id;
            view.date = (DateTime)relation.createDateProper();
            view.userId = relation.entityId1;
            view.contentId = -relation.entityId2;
            view.type = relation.type.Substring(Keys.ActivityKey.Length, TypeLength);
            view.contentType = relation.type.Substring(Keys.ActivityKey.Length + TypeLength); //Skip the actual activity type, it starts the type field
            view.action = relation.value.Substring(0, 2); //Assume it's 1 character, but skip first
            view.extra = relation.value.Substring(2);

            return view;
        }

        public override EntityRelation FromView(ActivityView view)
        {
            var relation = new EntityRelation();
            relation.entityId1 = view.userId;
            relation.entityId2 = -view.contentId; //It has to be NEGATIVE because we don't want them linked to content
            relation.createDate = view.date;
            relation.type = Keys.ActivityKey + view.type + (view.contentType ?? ""); 
            relation.value = view.action;
            relation.id = view.id;

            if(!string.IsNullOrWhiteSpace(view.extra))
                relation.value += view.extra;

            return relation;
        }

        public override EntityRelationSearch CreateSearch(ActivitySearch search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += (search.Type ?? "%");
            return es;
        }

        public override IQueryable<long> FinalizeQuery(IQueryable<EntityGroup> query, ActivitySearch search)  
        {
            if(search.ContentLimit.Limit.Count > 0)
                return SimpleMultiLimit(query, search.ContentLimit.Limit, (e) => -e.entityId2);

            return base.FinalizeQuery(query, search);
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public override Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return provider.GetListAsync(GetByIds<EntityRelation>(ids));
        }
    }
}