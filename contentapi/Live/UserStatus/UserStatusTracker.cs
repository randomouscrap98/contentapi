using System.Collections.Concurrent;
using contentapi.data;

namespace contentapi.Live;

public class UserStatusTracker : IUserStatusTracker
{
    protected ILogger logger;
    protected ConcurrentDictionary<long, UserStatusCollection> statuses = new ConcurrentDictionary<long, UserStatusCollection>();

    public event Func<long, Task>? StatusUpdated; //EventHandler<long>? StatusUpdated;

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
    public async Task<Tuple<int, bool>> AddStatusAsync(long userId, long contentId, string? status, int trackerId)
    {
        var statusCollection = statuses.GetOrAdd(contentId, x => new UserStatusCollection());

        int removeCount = 0;
        bool added = false;

        //Must be done OUTSIDE the lock!!
        var originalStatus = await GetStatusForUserAsync(contentId, userId);

        await statusCollection.CollectionLock.WaitAsync();

        try
        {
            //Need to remove any old statuses only for OUR tracker
            removeCount = statusCollection.Statuses.RemoveAll(x => x.userId == userId && x.trackerId == trackerId);

            //This allows us to remove statuses by setting them to null or otherwise.
            if(!string.IsNullOrWhiteSpace(status))
            {
                added = true;

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

        //If the current set status is specifically different than the previous status, report it as a status "update". 
        //This prevents repeated setting of the user status from creating an event flood
        if(originalStatus != status && StatusUpdated != null)
            await StatusUpdated.Invoke(contentId);
        //if(!(removeCount == 0 && !added))
        //    if(StatusUpdated != null)
        //        await StatusUpdated.Invoke(contentId);

        return Tuple.Create(removeCount, added);
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
    public async Task<Dictionary<long, Dictionary<long, string>>> GetUserStatusesAsync(params long[] contentIds) 
    {
        var result = new Dictionary<long, Dictionary<long, string>>();

        //If it's empty, get them all
        var searchKeys = contentIds.Length > 0 ? contentIds.ToList() : statuses.Keys.ToList();

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
    /// Retrieve the single user status for the given content, which might be null
    /// </summary>
    /// <param name="contentId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<string?> GetStatusForUserAsync(long contentId, long userId)
    {
        var statusCollection = statuses.GetOrAdd(contentId, x => new UserStatusCollection());

        await statusCollection.CollectionLock.WaitAsync();

        try
        {
            return statusCollection.Statuses.LastOrDefault(x => x.userId == userId)?.status;
        }
        finally
        {
            statusCollection.CollectionLock.Release();
        }
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
        List<long> updated = new List<long>();

        foreach(var key in statuses.Keys.ToList())
        {
            //It's better to waste computation here than to waste even more resources alerting for 
            //rooms that didn't even change anything. Also, always must be done OUTSIDE the lock!!
            var originalStatuses = await GetStatusForContentAsync(key);
            int thisRemoved = 0;

            //Attempt to remove the status within a lock. We can't do anything else inside that lock
            if(statuses.TryGetValue(key, out statusCollection))
            {
                await statusCollection!.CollectionLock.WaitAsync();

                try
                {
                    thisRemoved = statusCollection!.Statuses.RemoveAll(x => x.trackerId == trackerId);
                }
                finally
                {
                    statusCollection!.CollectionLock.Release();
                }
            }

            if(thisRemoved > 0)
            {
                removed.Add(key, thisRemoved);

                //Check more particularly for list updates, OUTSIDE the lock!
                var newStatuses = await GetStatusForContentAsync(key);
                if(!(originalStatuses.Count == newStatuses.Count && originalStatuses.All(
                    (d1KV) => newStatuses.TryGetValue(d1KV.Key, out var d2Value) && (
                        d1KV.Value == d2Value))))
                {
                    updated.Add(key);
                }
            }
        }

        foreach(var contentId in updated)
        {
            if(StatusUpdated != null)
                await StatusUpdated.Invoke(contentId);
        }

        return removed;
    }
}