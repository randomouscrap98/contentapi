using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class ActivitySearch : BaseSearch
    {
        public List<long> UserIds {get;set;} = new List<long>();
        public List<long> ContentIds {get;set;} = new List<long>();

        public string Type {get;set;}
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

    public class ActivityViewSource : BaseRelationViewSource, IViewSource<ActivityView, EntityRelation, EntityGroup, ActivitySearch>
    {
        public ActivityViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override string EntityType => Keys.ActivityKey;

        /// <summary>
        /// Produce an activity for the given entity and action. Can include ONE piece of extra data.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="action"></param>
        /// <param name="extra"></param>
        /// <returns></returns>
        public EntityRelation MakeActivity(Entity entity, long user, string action, string extra = null)
        {
            return FromView(new ActivityView()
            {
                id = 0,
                userId = user,
                contentId = entity.id,
                action = action,
                extra = extra,
                contentType = entity.type,
                date = DateTime.Now
            });
        }

        public ActivityView ToView(EntityRelation relation)
        {
            var view = new ActivityView();

            view.id = relation.id;
            view.date = (DateTime)relation.createDateProper();
            view.userId = relation.entityId1;
            view.contentId = -relation.entityId2;
            view.contentType = relation.type.Substring(Keys.ActivityKey.Length); //Skip the actual activity type, it starts the type field
            view.action = relation.value.Substring(0, 1); //Assume it's 1 character
            view.extra = relation.value.Substring(1);

            return view;
        }

        public EntityRelation FromView(ActivityView view)
        {
            var activity = new EntityRelation();
            activity.entityId1 = view.userId;
            activity.entityId2 = -view.contentId; //It has to be NEGATIVE because we don't want them linked to content
            activity.createDate = view.date;
            activity.type = Keys.ActivityKey + view.contentType; 
            activity.value = view.action;
            activity.id = view.id;

            if(!string.IsNullOrWhiteSpace(view.extra))
                activity.value += view.extra;

            return activity;
        }

        public override EntityRelationSearch CreateSearch<S>(S search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += ((search as ActivitySearch).Type ?? "%");
            return es;
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return provider.GetListAsync(GetByIds<EntityRelation>(ids));
        }

        public IQueryable<long> SearchIds(ActivitySearch search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search, x => -x.entityId2);

            if(modify != null)
                query = modify(query);
            
            return FinalizeQuery(query, search, x => x.relation.id);
        }
    }
}