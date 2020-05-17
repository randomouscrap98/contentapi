using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Extensions
{
    public static class IViewSourceExtensions
    {
        public static Task<List<T>> SimpleSearchRawAsync<V,T,E,S>(
            this IViewSource<V,T,E,S> source, S search, Func<IQueryable<E>, IQueryable<E>> modify = null) 
            where V : IIdView where S : IIdSearcher where E : EntityGroup
        {
            var ids = source.SearchIds(search, modify);
            return source.RetrieveAsync(ids);
        }

        public static async Task<List<V>> SimpleSearchAsync<V,T,E,S>(
            this IViewSource<V,T,E,S> source, S search, Func<IQueryable<E>, IQueryable<E>> modify = null) 
            where V : IIdView where S : IIdSearcher where E : EntityGroup
        {
            return (await source.SimpleSearchRawAsync(search, modify)).Select(x => source.ToView(x)).ToList();
        }

        public static async Task<T> FindByIdRawAsync<V,T,E,S>(
            this IViewSource<V,T,E,S> source, long id, Func<IQueryable<E>, IQueryable<E>> modify = null) 
            where V : IIdView where S : IIdSearcher, new() where E : EntityGroup
        {
            var search = new S();
            search.Ids.Add(id);
            return (await source.SimpleSearchRawAsync(search, modify)).OnlySingle();
        }

        public static async Task<V> FindByIdAsync<V,T,E,S>(
            this IViewSource<V,T,E,S> source, long id, Func<IQueryable<E>, IQueryable<E>> modify = null) 
            where V : IIdView where S : IIdSearcher, new() where E : EntityGroup
        {
            var search = new S();
            search.Ids.Add(id);
            return (await source.SimpleSearchAsync(search, modify)).OnlySingle();
        }

        //public static IQueryable<KeyedAggregateData> SimpleAggregate<V,T,E,S,R>(
        //    this IViewSource<V,T,E,S> source, S search, Expression<Func<R, long>> keySelector) 
        //    where V : IIdView where S : IIdSearcher, new() where E : EntityGroup

        //public static IQueryable<KeyedAggregateData> SimpleAggregate<V,T,E,S>(
        //    this IViewSource<V,T,E,S> source, S search, Expression<Func<EntityRelation, long>> keySelector) 
        //    where V : IIdView where S : IIdSearcher, new() where E : EntityGroup
        //{
        //        from i in watchSource.SearchIds(new WatchSearch() { ContentIds = baseResult.Select(x => x.id).ToList() })
        //        join r in Q<EntityRelation>() on i equals r.id
        //        group r by r.entityId2 into g
        //        select new { id = g.Key, count = g.Count() });
        //}
    }
}