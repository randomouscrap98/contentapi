
using contentapi.Search;

namespace contentapi.Live;

public class UserlistResult
{
    public Dictionary<long, Dictionary<long, string>> statuses = new Dictionary<long, Dictionary<long, string>>();
    public Dictionary<string, IEnumerable<IDictionary<string, object>>> objects = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
}

public static class LiveExtensions
{
    /// <summary>
    /// Get all statuses the given user is allowed to retrieve.
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public static async Task<UserlistResult> GetUserStatusesAsync(this IUserStatusTracker userStatuses, 
        IGenericSearch searcher, long uid, string contentFields = "*", string userFields = "*", params long[] contentIds)
    {
        //Always allow 0 in there FYI
        var allStatuses = await userStatuses.GetUserStatusesAsync(contentIds);
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
        foreach(var id in allIds.Where(x => x != 0).Except(allSearch.objects["content"].Select(x => (long)x["id"])))
        {
            allStatuses.Remove(id);
        }

        //Don't leak information: remove users that aren't referenced
        var allUsers = allStatuses.SelectMany(x => x.Value.Keys).Union(allSearch.objects["content"].Select(x => (long)x["createUserId"]));
        allSearch.objects["user"] = allSearch.objects["user"].Where(x => allUsers.Contains((long)x["id"]));

        return new UserlistResult()
        {
            statuses = allStatuses,
            objects = allSearch.objects
        };
    }

    //public static async Task<int> AddStatusAsync(this IUserStatusTracker userStatuses, ILiveEventQueue queue, 
    //    long uid, long contentId, string status, int trackerId)
    //{
    //    var replaced = await userStatuses.AddStatusAsync(uid, contentId, status, trackerId);

    //    //Regardless of the change (not optimized), create an event
    //    await queue.AddEventAsync(new LiveEvent()
    //    {
    //        userId = uid,
    //        type = EventType.userlist,
    //        action = replaced == 0 ? Db.UserAction.create : Db.UserAction.update,
    //        refId = contentId
    //    });

    //    return replaced;
    //}

    //public static async Task<Dictionary<long, int>> RemoveStatusesByTrackerAsync(this IUserStatusTracker userStatuses, long uid, int trackerId, ILiveEventQueue queue)
    //{
    //    var removed = await userStatuses.RemoveStatusesByTrackerAsync(trackerId);

    //    foreach(var contentId in removed.Keys.ToList())
    //    {
    //        await queue.AddEventAsync(new LiveEvent()
    //        {
    //            userId = uid,
    //            type = EventType.userlist,
    //            action = Db.UserAction.delete,
    //            refId = contentId
    //        });
    //    }

    //    return removed;
    //}
}