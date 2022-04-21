using contentapi.Views;
using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Search;

public interface IGenericSearch
{
    //NOTE: in the past, I wanted this to be able to return all different kinds of results.
    //Now, I would like it to be far more simplified, as much of the aggregation can be 
    //done client side. The biggest push to have just "objects" was the desire for an endpoint
    //which produced a tree, but the amount of cons it brings us far outweigh the pros imo,
    //so I'd rather just give out these basic lists of objects all the time and have
    //other endpoints which consume this have a MUCH easier time.
    Task<GenericSearchResult> SearchUnrestricted(SearchRequests requests);
    Task<GenericSearchResult> Search(SearchRequests requests, long requestUserId = 0);

    /// <summary>
    /// Lookup type by id; throws "NotFoundException" if type with given id not found
    /// </summary>
    /// <param name="type"></param>
    /// <param name="id"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [Obsolete]
    Task<T> GetById<T>(RequestType type, long id, bool throwIfDeleted = false);
    Task<T> GetById<T>(long id, bool throwIfDeleted = false);
    Task<List<T>> GetByField<T>(RequestType type, string fieldname, object value, string comparator = "=");

    //DEFINITELY used for internal tasks!
    Task<QueryResultSet> QueryRawAsync(string sql, Dictionary<string, object> values);

    List<T> ToStronglyTyped<T>(QueryResultSet singleResults);
}