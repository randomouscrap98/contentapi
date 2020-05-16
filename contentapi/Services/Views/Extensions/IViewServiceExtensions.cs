using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Views;

namespace contentapi.Services.Views.Extensions
{
    public static class IViewServiceExtensions
    {
        public static async Task<V> FindByIdAsync<V,T,E,S>(this IViewService<V,S> service, long id, Requester requester)
            where V : IIdView where S : IIdSearcher, new()
        {
            var search = new S();
            search.Ids.Add(id);
            return (await service.SearchAsync(search, requester)).OnlySingle();
        }
    }
}