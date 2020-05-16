using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Views;

namespace contentapi.Services.Views.Extensions
{
    public static class GenericViewSourceExtensions
    {
        public static async Task<List<V>> SimpleSearch<V,T,E,S>(
            this IViewSource<V,T,E,S> source, S search, Func<IQueryable<E>, IQueryable<E>> modify = null) 
            where V : IIdView where S : IIdSearcher where E : EntityGroup
        {
            var ids = source.SearchIds(search, modify);
            var baseObjects = await source.RetrieveAsync(ids);
            return baseObjects.Select(x => source.ToView(x)).ToList();
        }

        public static async Task<V> FindByIdAsync<V,T,E,S>(
            this IViewSource<V,T,E,S> source, long id, Func<IQueryable<E>, IQueryable<E>> modify = null) 
            where V : IIdView where S : IIdSearcher, new() where E : EntityGroup
        {
            var search = new S();
            search.Ids.Add(id);
            return (await source.SimpleSearch(search, modify)).OnlySingle();
        }
    }
}