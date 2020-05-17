using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public abstract class BaseRelationViewSource<V,T,E,S> : BaseViewSource<V,T,E,S>
        where V : BaseView where E : EntityGroup, new() where S : EntitySearchBase, IConstrainedSearcher
    {
        public BaseRelationViewSource(ILogger<BaseViewSource<V,T,E,S>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public abstract string EntityType {get;}
        public abstract Expression<Func<EntityRelation, long>> PermIdSelector {get;}

        public virtual EntityRelationSearch CreateSearch(S search)
        {
            var relationSearch = mapper.Map<EntityRelationSearch>(search);
            relationSearch.TypeLike = EntityType;
            return relationSearch;
        }

        public override IQueryable<E> GetBaseQuery(S search)
        {
            var entitySearch = CreateSearch(search);

            return provider.ApplyEntityRelationSearch(Q<EntityRelation>(), entitySearch, false)
                .Join(Q<EntityRelation>(), PermIdSelector, r => r.entityId2, 
                (r1, r2) => new E() { relation = r1, permission = r2});
        }
    }
}