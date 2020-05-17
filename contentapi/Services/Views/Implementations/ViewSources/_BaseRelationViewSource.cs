using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public abstract class BaseRelationViewSource : BaseViewSource
    {
        public BaseRelationViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public abstract string EntityType {get;}

        public virtual EntityRelationSearch CreateSearch<S>(S search) where S : BaseSearch
        {
            var relationSearch = LimitSearch(mapper.Map<EntityRelationSearch>(search));
            relationSearch.TypeLike = EntityType;
            return relationSearch;
        }

        public virtual IQueryable<EntityGroup> GetBaseQuery<S>(S search, Expression<Func<EntityRelation,long>> permIdSelector) where S : BaseSearch
        {
            var entitySearch = CreateSearch(search);

            return provider.ApplyEntityRelationSearch(Q<EntityRelation>(), entitySearch, false)
                .Join(Q<EntityRelation>(), permIdSelector, r => r.entityId2, 
                (r1, r2) => new EntityGroup() { relation = r1, permission = r2});
        }

        //public virtual Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        //{
        //    return provider.GetListAsync(GetByIds<EntityRelation>(ids));
        //}

        //public Task<List<EntityRelation>> RetrieveAsync(IQueryable<EntityRelation> items)
        //{
        //    return provider.GetListAsync(items);
        //}
    }
}