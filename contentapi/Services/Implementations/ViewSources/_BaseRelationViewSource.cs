using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public abstract class BaseRelationViewSource<V,T,E,S> : BaseViewSource<V,T,E,S>
        where V : BaseView where E : EntityGroup, new() where S : EntitySearchBase, IConstrainedSearcher
    {
        public BaseRelationViewSource(ILogger<BaseRelationViewSource<V,T,E,S>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public abstract string EntityType {get;}
        public abstract Expression<Func<EntityRelation, long>> PermIdSelector {get;}
        public override Expression<Func<E, long>> MainIdSelector => x => x.relation.id;

        public virtual EntityRelationSearch CreateSearch(S search)
        {
            var relationSearch = mapper.Map<EntityRelationSearch>(search);
            relationSearch.TypeLike = EntityType;
            return relationSearch;
        }

        public override async Task<IQueryable<E>> GetBaseQuery(S search)
        {
            var entitySearch = CreateSearch(search);

            return provider.ApplyEntityRelationSearch(await Q<EntityRelation>(), entitySearch, false)
                .Join(await Q<EntityRelation>(), PermIdSelector, r => r.entityId2, 
                (r1, r2) => new E() { relation = r1, permission = r2});
        }


        public async Task<IQueryable<long>> SimpleMultiLimit(IQueryable<E> query, IEnumerable<IdLimit> limit, Func<EntityRelation, long> limitExpression)
        {
            //join query with watches, select query where id > watch id
            var ids = query
                .GroupBy(MainIdSelector).Select(x => new EntityRelation() { 
                    id = x.Max(y => y.relation.id), 
                    entityId1 = x.Max(y => y.relation.entityId1),
                    entityId2 = x.Max(y => y.relation.entityId2)})
                .AsEnumerable()
                .Join(limit, limitExpression, l => l.id, (r, l) => new { r = r, l = l })
                .Where(x => x.r.id > x.l.min)
                .Select(x => x.r.id);
                //join l in limit on q.limitId equals l.id
                //where q.id > l.min
                //select q;

            //var ids = crap.Select(x => x.id);

            return (await Q<EntityRelation>()).Where(x => ids.Contains(x.id)).Select(x => x.id);
        }
    }
}