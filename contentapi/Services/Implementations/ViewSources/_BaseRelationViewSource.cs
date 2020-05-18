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
    public abstract class BaseRelationViewSource<V,T,S> : BaseViewSource<V,T,S>
        where V : BaseView where S : EntitySearchBase, IConstrainedSearcher
    {
        public BaseRelationViewSource(ILogger<BaseRelationViewSource<V,T,S>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public abstract string EntityType {get;}
        public abstract Expression<Func<EntityRelation, long>> PermIdSelector {get;}
        public override Expression<Func<EntityGroup, long>> MainIdSelector => x => x.relation.id;

        public virtual EntityRelationSearch CreateSearch(S search)
        {
            var relationSearch = mapper.Map<EntityRelationSearch>(search);
            relationSearch.TypeLike = EntityType;
            return relationSearch;
        }

        public override IQueryable<EntityGroup> GetBaseQuery(S search)//, Expression<Func<EntityRelation, long>> permIdSelector)
        {
            var entitySearch = CreateSearch(search);

            return provider.ApplyEntityRelationSearch(Q<EntityRelation>(), entitySearch, false)
                .Join(Q<EntityRelation>(), x => -x.entityId2, r => r.entityId2, 
                (r1, r2) => new EntityGroup() { relation = r1, permission = r2});
        }
    }
}