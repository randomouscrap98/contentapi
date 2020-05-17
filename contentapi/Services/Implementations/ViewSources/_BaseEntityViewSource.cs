using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public abstract class BaseEntityViewSource<V,T,E,S> : BaseViewSource<V,T,E,S>
        where V : BaseView where E : EntityGroup, new() where S : BaseHistorySearch, IConstrainedSearcher where T : EntityPackage
    {
        public BaseEntityViewSource(ILogger<BaseViewSource<V,T,E,S>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public abstract string EntityType {get;}

        public override async Task<List<T>> RetrieveAsync(IQueryable<long> ids)
        {
            return (await provider.LinkAsync(GetByIds<Entity>(ids))).Cast<T>().ToList();
        }
        
        public virtual EntitySearch CreateSearch(S search) //where S : BaseSearch
        {
            var entitySearch = mapper.Map<EntitySearch>(search);
            entitySearch.TypeLike = EntityType;
            return entitySearch;
        }

        public override IQueryable<E> GetBaseQuery(S search)
        {
            var entitySearch = CreateSearch(search);

            return provider.ApplyEntitySearch(Q<Entity>(), entitySearch, false)
                .Join(Q<EntityRelation>(), e => e.id, r => r.entityId2, 
                (e, r) => new E() { entity = e, permission = r});
        }

        public override IQueryable<E> ModifySearch(IQueryable<E> query, S search)
        {
            return LimitByCreateEdit(base.ModifySearch(query, search), search.CreateUserIds, search.EditUserIds);
        }
    }
}