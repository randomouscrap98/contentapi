using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public abstract class BaseEntityViewSource : BaseViewSource
    {
        public BaseEntityViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public abstract string EntityType {get;}

        public virtual Task<List<EntityPackage>> RetrieveAsync(IQueryable<long> ids)
        {
            return provider.LinkAsync(GetByIds<Entity>(ids));
        }
        
        public virtual EntitySearch CreateSearch<S>(S search) where S : BaseSearch
        {
            var entitySearch = LimitSearch(mapper.Map<EntitySearch>(search));
            entitySearch.TypeLike = EntityType;
            return entitySearch;
        }

        public virtual IQueryable<EntityGroup> GetBaseQuery<S>(S search) where S : BaseSearch
        {
            var entitySearch = CreateSearch(search); //, extraType);

            return provider.ApplyEntitySearch(Q<Entity>(), entitySearch, false)
                .Join(Q<EntityRelation>(), e => e.id, r => r.entityId2, 
                (e, r) => new EntityGroup() { entity = e, permission = r});
        }
    }
}