using contentapi.data;

namespace contentapi.Search;

public static class SearchExtensions
{
    /// <summary>
    /// A shortcut for searching for a list of a single type WITH casting. 
    /// </summary>
    /// <param name="search"></param>
    /// <param name="request"></param>
    /// <param name="values"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async Task<List<T>> SearchSingleTypeUnrestricted<T>(this IGenericSearch search, SearchRequest request, Dictionary<string, object>? values = null)
    {
        //Now go get some random-ass module message
        var searchResult = await search.SearchUnrestricted(new SearchRequests() {
            values = values ?? new Dictionary<string, object>(),
            requests = new List<SearchRequest> { request }
        });

        return search.ToStronglyTyped<T>(searchResult.objects.First().Value);
    }

    public static async Task<List<T>> SearchSingleType<T>(this IGenericSearch search, long uid, SearchRequest request, Dictionary<string, object>? values = null)
    {
        //Now go get some random-ass module message
        var searchResult = await search.Search(new SearchRequests() {
            values = values ?? new Dictionary<string, object>(),
            requests = new List<SearchRequest> { request }
        }, uid);

        return search.ToStronglyTyped<T>(searchResult.objects.First().Value);
    }

    public static string GetDatabaseForType<T>(this IViewTypeInfoService typeService)
    {
        var typeinfo = typeService.GetTypeInfo<T>();
        return typeinfo.selectFromSql ?? throw new InvalidOperationException($"No database for type {typeof(T).Name}");
    }    
}