using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public abstract class BaseViewSource<V,T,S> : ViewSourceServices, IViewSource<V,T,EntityGroup,S> 
        where V : BaseView where S : EntitySearchBase, IConstrainedSearcher
    {
        protected IMapper mapper;
        protected ILogger logger;
        protected IEntityProvider provider;

        public abstract Expression<Func<EntityGroup, long>> MainIdSelector {get;}

        public BaseViewSource(ILogger<BaseViewSource<V,T,S>> logger, IMapper mapper, IEntityProvider provider)
        {
            this.logger = logger;
            this.mapper = mapper;
            this.provider = provider;
        }

        public IQueryable<X> Q<X>() where X : EntityBase
        {
            return provider.GetQueryable<X>();
        }

        /// <summary>
        /// Modify the given query such that only those with the given parents are returned
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parentIds"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public IQueryable<EntityGroup> LimitByParents(IQueryable<EntityGroup> query, List<long> parentIds)
        {
            return  from q in query
                    join r in Q<EntityRelation>() on q.entity.id equals r.entityId2
                    where r.type == Keys.ParentRelation && parentIds.Contains(r.entityId1)
                    select q;
        }

        /// <summary>
        /// Modify the given query such that only those with matching values are returned
        /// </summary>
        /// <param name="query"></param>
        /// <param name="keyLike"></param>
        /// <param name="valueLike"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public IQueryable<EntityGroup> LimitByValue(IQueryable<EntityGroup> query, string keyLike, string valueLike)
        {
            return  from q in query
                    join v in Q<EntityValue>() on q.entity.id equals v.entityId
                    where EF.Functions.Like(v.key, keyLike) && EF.Functions.Like(v.value, valueLike)
                    select q;
        }

        public IQueryable<EntityGroup> LimitByCreateEdit(IQueryable<EntityGroup> query, List<long> creators, List<long> editors)
        {
            var editorStrings = editors.Select(x => x.ToString());

            return  from q in query
                    join r in Q<EntityRelation>() on q.entity.id equals r.entityId2
                    where (creators.Count == 0 || creators.Contains(r.entityId1)) && 
                          (editors.Count == 0 || editorStrings.Contains(r.value))
                    select q;
        }

        public IQueryable<X> GetByIds<X>(IQueryable<long> ids) where X : EntityBase
        {
            return
                from e in provider.GetQueryable<X>()
                join i in ids on e.id equals i
                select e;
        }

        public virtual IQueryable<long> FinalizeQuery(IQueryable<EntityGroup> query, S search, Expression<Func<EntityGroup,long>> mainIdSelector) //where S : EntitySearchBase
        {
            var husks = query.GroupBy(mainIdSelector).Select(x => new EntityBase() { id = x.Key });

            //Note: applyfinal finalizes some limiters (such as skip/take) and ALSO tries to apply
            //the fallback ordering. This is ID and random, which we don't need to implement up here.
            return provider.ApplyFinal(husks, search).Select(x => x.id);
        }

        public async Task<Dictionary<X, SimpleAggregateData>> GroupAsync<R,X>(IQueryable<long> ids, Expression<Func<R,X>> keySelector) where R : EntityBase
        {
            var pureList = await provider.GetListAsync(
                ids.Join(Q<R>(), x => x, r => r.id, (x, r) => r).GroupBy(keySelector).Select(g => new 
                { 
                    key = g.Key, 
                    aggregate = new SimpleAggregateData()
                    {
                        count = g.Count(),
                        lastDate = g.Max(x => x.createDate),
                        firstDate = g.Min(x => x.createDate)
                    }
                })
            );
            
            return pureList.ToDictionary(x => x.key, y => y.aggregate);
        }

        public abstract IQueryable<EntityGroup> GetBaseQuery(S search);
        public abstract Task<List<T>> RetrieveAsync(IQueryable<long> ids);
        public abstract V ToView(T basic);
        public abstract T FromView(V view);

        public virtual IQueryable<EntityGroup> ModifySearch(IQueryable<EntityGroup> query, S search) { return query; }
        public virtual IQueryable<EntityGroup> OrderSearch(IQueryable<EntityGroup> query, S search) { return query; }

        public IQueryable<long> SearchIds(S search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search);

            query = ModifySearch(query, search);

            if(modify != null)
                query = modify(query);

           //Special sorting routines go here
           query = OrderSearch(query, search);

            return FinalizeQuery(query, search, MainIdSelector); //x => x.entity.id);
        }
    }
}