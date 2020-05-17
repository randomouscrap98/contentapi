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
    public class WatchSearch : BaseSearch
    {
        public List<long> UserIds {get;set;} = new List<long>();
        public List<long> ContentIds {get;set;} = new List<long>();
    }

    public class WatchViewServiceProfile : Profile 
    {
        public WatchViewServiceProfile()
        {
            //Only map direct fields which are the same. We lose contentid and other things... perhaps
            //they will be added later.
            CreateMap<WatchSearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.UserIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.ContentIds.Select(x => -x).ToList()));
        }
    }

    public class WatchViewSource : BaseRelationViewSource, IViewSource<WatchView, EntityRelation, EntityGroup, WatchSearch>
    {
        public WatchViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override string EntityType => Keys.WatchRelation;

        public EntityRelation FromView(WatchView view)
        {
            var relation = new EntityRelation()
            {
                value = view.lastNotificationId.ToString(),
                entityId1 = view.userId,
                entityId2 = -view.contentId
            };

            this.ApplyFromBaseView(view, relation);

            return relation;
        }

        public WatchView ToView(EntityRelation basic)
        {
            var view = new WatchView()
            {
                lastNotificationId = long.Parse(basic.value),
                userId = basic.entityId1,
                contentId = -basic.entityId2
            };

            this.ApplyToBaseView(basic, view);

            return view;
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return provider.GetListAsync(GetByIds<EntityRelation>(ids));
        }

        public IQueryable<long> SearchIds(WatchSearch search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search, x => -x.entityId2);

            if(modify != null)
                query = modify(query);
            
            return FinalizeQuery(query, search, x => x.relation.id);
        }
    }
}