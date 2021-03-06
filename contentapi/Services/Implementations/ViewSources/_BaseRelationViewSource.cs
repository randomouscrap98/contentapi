using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public abstract class BaseRelationViewSource<V,T,E,S> : BaseViewSource<V,T,E,S>
        where V : BaseView where E : EntityGroup, new() where S : EntitySearchBase, IConstrainedSearcher
    {
        public BaseRelationViewSource(ILogger<BaseRelationViewSource<V,T,E,S>> logger, BaseViewSourceServices services)
            : base(logger, services) { }
        
        public abstract string EntityType {get;}
        public abstract Expression<Func<EntityRelation, long>> PermIdSelector {get;}
        public override Expression<Func<E, long>> MainIdSelector => x => x.relation.id;

        //TODO: THIS NEEDS TO BE REWRITTEN!!
        public bool JoinPermissions = true;

        public virtual EntityRelationSearch CreateSearch(S search)
        {
            var relationSearch = services.mapper.Map<EntityRelationSearch>(search);
            relationSearch.TypeLike = EntityType;
            return relationSearch;
        }

        public override async Task<IQueryable<E>> GetBaseQuery(S search)
        {
            var entitySearch = CreateSearch(search);

            if(JoinPermissions)
            {
                return services.provider.ApplyEntityRelationSearch(await Q<EntityRelation>(), entitySearch, false)
                    .Join(await Q<EntityRelation>(), PermIdSelector, r => r.entityId2,
                    (r1, r2) => new E() { relation = r1, permission = r2 });
            }
            else
            {
                return services.provider.ApplyEntityRelationSearch(await Q<EntityRelation>(), entitySearch, false).Select(r => new E() { relation = r});
            }
        }


        public async Task<IQueryable<long>> SimpleMultiLimit(IQueryable<E> query, IEnumerable<IdLimit> limit, Func<EntityRelation, long> limitExpression)
        {
            //join query with watches, select query where id > watch id
            var ids = query
                .GroupBy(MainIdSelector).Select(x => new EntityRelation() { 
                    id = x.Max(y => y.relation.id), 
                    entityId1 = x.Max(y => y.relation.entityId1),
                    entityId2 = x.Max(y => y.relation.entityId2)})
                .AsEnumerable() //This is done IN MEMORY from this point!! Isn't it?? You can't join against an IEnumerable from a database, can you?
                .Join(limit, limitExpression, l => l.id, (r, l) => new { r = r, l = l })
                .Where(x => x.r.id > x.l.min)
                .Select(x => x.r.id);

            return (await Q<EntityRelation>()).Where(x => ids.Contains(x.id)).Select(x => x.id);
        }
    }
}