using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Views;

namespace contentapi.Services.Extensions
{
    public static class IViewServiceExtensions
    {
        public static async Task<V> FindByIdAsync<V,S>(this IViewReadService<V,S> service, long id, Requester requester)
            where V : IIdView where S : IConstrainedSearcher, new()
        {
            var search = new S();
            search.Ids.Add(id);
            return (await service.SearchAsync(search, requester)).OnlySingle();
        }
    }
}