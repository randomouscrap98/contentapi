using System.Collections.Concurrent;
using contentapi.data;

namespace contentapi.Live;

public class UserStatusTracker : IUserStatusTracker
{
    protected ILogger logger;
    protected ConcurrentDictionary<long, UserStatusCollection> statuses = new ConcurrentDictionary<long, UserStatusCollection>();

    public UserStatusTracker(ILogger<UserStatusTracker> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Add the given status for the given user in the given content. TrackerId is the system that asked,
    /// should always be the same for the same system (for instance, same websocket)
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="contentId"></param>
    /// <param name="status"></param>
    /// <param name="trackerId"></param>
    /// <returns></returns>
    public async Task<int> AddStatusAsync(long userId, long contentId, string? status, int trackerId)
    {
        var statusCollection = statuses.GetOrAdd(contentId, x => new UserStatusCollection());

        await statusCollection.CollectionLock.WaitAsync();

        int removeCount = 0;

        try
        {
            //Need to remove any old statuses only for OUR tracker
            removeCount = statusCollection.Statuses.RemoveAll(x => x.userId == userId && x.trackerId == trackerId);

            //This allows us to remove statuses by setting them to null or otherwise.
            if(!string.IsNullOrWhiteSpace(status))
            {
                //Always add to end
                statusCollection.Statuses.Add(new UserStatus()
                {
                    userId = userId,
                    status = status,
                    trackerId = trackerId
                });
            }
        }
        finally
        {
            statusCollection.CollectionLock.Release();
        }

        return removeCount;
    }

    /// <summary>
    /// Get ALL statuses regardless of permissions or anything, just literally all statuses for
    /// all content that we're tracking. 
    /// </summary>
    /// <remarks>
    /// Even if the API is running for a very long time, this should still be relatively small, 
    /// since it should only represent the statuses for users currently connected.
    /// </remarks>
    /// <returns></returns>
    public async Task<Dictionary<long, Dictionary<long, string>>> GetUserStatusesAsync(params long[] contentIds) //IEnumerable<long>? contentIds = null)
    {
        var result = new Dictionary<long, Dictionary<long, string>>();

        //If it's empty, get them all
        var searchKeys = contentIds.Length > 0 ? contentIds.ToList() : statuses.Keys.ToList();
        //var searchKeys = contentIds.Length > 0 ? statuses.Keys.Intersect(contentIds) : statuses.Keys.ToList();

        //Use tolist to ensure that the keys don't change from underneath us
        foreach(var key in searchKeys)
        {
            var contentResult = await GetStatusForContentAsync(key);

            //if the user specified a specific list of contentids, always return SOMETHING on the list...
            if(contentIds.Length > 0 || contentResult.Count > 0)
                result.Add(key, contentResult);
        }

        return result;
    }

    /// <summary>
    /// Compute the apparent statuses for each user reported inside the given contentId. For this system,
    /// the "apparent" status is the one added most recently. So, if two trackers add a status for a user,
    /// the tracker that adds it last will get their status reported.
    /// </summary>
    /// <param name="contentId"></param>
    /// <returns></returns>
    public async Task<Dictionary<long, string>> GetStatusForContentAsync(long contentId)
    {
        //Auto-adds the key no matter what!
        var statusCollection = statuses.GetOrAdd(contentId, x => new UserStatusCollection());
        var result = new Dictionary<long, string>();

        await statusCollection.CollectionLock.WaitAsync();

        try
        {
            //A VERY SIMPLE loop which ensures that the LAST status added is the one that
            //is reported, because it goes in order! We might waste a lot of assignments,
            //but heck it might actually be faster to do it this way than a smarter way in practice.
            //No if statements is a powerful thing!
            foreach(var status in statusCollection.Statuses)
                result[status.userId] = status.status;
        }
        finally
        {
            statusCollection.CollectionLock.Release();
        }

        return result;
    }

    /// <summary>
    /// Remove every single status reported by the given tracker across all rooms
    /// </summary>
    /// <param name="trackerId"></param>
    /// <returns>The amount of statuses removed per content</returns>
    public async Task<Dictionary<long, int>> RemoveStatusesByTrackerAsync(int trackerId)
    {
        UserStatusCollection? statusCollection;
        Dictionary<long, int> removed = new Dictionary<long, int>();

        foreach(var key in statuses.Keys.ToList())
        {
            if(statuses.TryGetValue(key, out statusCollection))
            {
                await statusCollection!.CollectionLock.WaitAsync();

                try
                {
                    var thisRemoved = statusCollection!.Statuses.RemoveAll(x => x.trackerId == trackerId);

                    if(thisRemoved > 0)
                        removed.Add(key, thisRemoved);
                }
                finally
                {
                    statusCollection!.CollectionLock.Release();
                }
            }
        }

        return removed;
    }
}