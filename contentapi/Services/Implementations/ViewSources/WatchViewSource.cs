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

    public class WatchViewSource : BaseRelationViewSource<WatchView, EntityRelation, EntityGroup, WatchSearch>
    {
        public WatchViewSource(ILogger<WatchViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override string EntityType => Keys.WatchRelation;
        public override Expression<Func<EntityRelation, long>> PermIdSelector => x => -x.entityId2;

        public EntityRelation HistoricCopy(EntityRelation relation, string type = null)
        {
            var history = new EntityRelation(relation);
            history.id = 0;
            history.createDate = DateTime.UtcNow;
            history.entityId1 = relation.id; //Link back! LINK BAKC!!!
            if(type != null) history.type = type;
            return history;
        }

        public override EntityRelation FromView(WatchView view)
        {
            var relation = new EntityRelation()
            {
                type = EntityType,
                value = view.lastNotificationId.ToString(),
                entityId1 = view.userId,
                entityId2 = -view.contentId
            };

            this.ApplyFromBaseView(view, relation);

            return relation;
        }

        public override WatchView ToView(EntityRelation basic)
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
        public override Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return provider.GetListAsync(GetByIds<EntityRelation>(ids));
        }
    }
}