
using contentapi.Search;

namespace contentapi.Live;

public class UserlistResult
{
    public Dictionary<long, Dictionary<long, string>> statuses = new Dictionary<long, Dictionary<long, string>>();
    public Dictionary<string, IEnumerable<IDictionary<string, object>>> data = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
}

public static class LiveExtensions
{
    /// <summary>
    /// Get all statuses the given user is allowed to retrieve.
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public static async Task<UserlistResult> GetAllStatusesAsync(this IUserStatusTracker userStatuses, 
        IGenericSearch searcher, long uid, string contentFields = "*", string userFields = "*")
    {
        //Always allow 0 in there FYI
        var allStatuses = await userStatuses.GetAllStatusesAsync();
        var allIds = allStatuses.Keys.ToList();

        //Search content AS THE USER so they only get the content they're allowed to get. Hopefully
        //there will never be an instance where there are over a thousand contents currently being watched
        var allSearch = await searcher.Search(new SearchRequests()
        {
            requests = new List<SearchRequest>()
            {
                new SearchRequest()
                {
                    type = "content",
                    fields = contentFields,
                    query = "id in @ids"
                },
                new SearchRequest()
                {
                    type = "user",
                    fields = userFields,
                    query = "id in @content.createUserId or id in @userIds"
                }
            },
            values = new Dictionary<string, object>()
            {
                { "ids", allIds },
                { "userIds", allStatuses.SelectMany(x => x.Value.Keys) }
            }
        }, uid);

        //Remove the statuses they can't see, meaning anything that's NOT in the allSearch set
        foreach(var id in allIds.Where(x => x != 0).Except(allSearch.data["content"].Select(x => (long)x["id"])))
            allStatuses.Remove(id);

        return new UserlistResult()
        {
            statuses = allStatuses,
            data = allSearch.data
        };
    }
}