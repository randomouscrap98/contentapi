namespace contentapi.Search;

public interface IGenericSearch
{
    //NOTE: in the past, I wanted this to be able to return all different kinds of results.
    //Now, I would like it to be far more simplified, as much of the aggregation can be 
    //done client side. The biggest push to have just "objects" was the desire for an endpoint
    //which produced a tree, but the amount of cons it brings us far outweigh the pros imo,
    //so I'd rather just give out these basic lists of objects all the time and have
    //other endpoints which consume this have a MUCH easier time.
    Task<Dictionary<string, IEnumerable<IDictionary<string, object>>>> Search(SearchRequests requests);
    Task<Dictionary<string, IEnumerable<IDictionary<string, object>>>> SearchRestricted(SearchRequests requests,
        long requestUserId = 0);

    List<T> ToStronglyTyped<T>(IEnumerable<IDictionary<string, object>> singleResults);
}