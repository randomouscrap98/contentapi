using System.Collections.Concurrent;

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
    public async Task AddStatusAsync(long userId, long contentId, string status, int trackerId)
    {
        var userStatus = new UserStatus()
        {
            userId = userId,
            status = status,
            trackerId = trackerId
        };

        var statusCollection = statuses.GetOrAdd(contentId, x => new UserStatusCollection());

        await statusCollection.CollectionLock.WaitAsync();

        try
        {
            //Need to remove any old statuses only for OUR tracker
            statusCollection.Statuses.RemoveAll(x => x.userId == userId && x.trackerId == trackerId);
            statusCollection.Statuses.Add(userStatus); //Always adds to end
        }
        finally
        {
            statusCollection.CollectionLock.Release();
        }
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
    public async Task<Dictionary<long, Dictionary<long, string>>> GetAllStatusesAsync()
    {
        var result = new Dictionary<long, Dictionary<long, string>>();

        //Use tolist to ensure that the keys don't change from underneath us
        foreach(var key in statuses.Keys.ToList())
        {
            var contentResult = await GetStatusForContentAsync(key);

            if(contentResult.Count > 0)
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
    /// <returns></returns>
    public async Task<int> RemoveStatusesByTrackerAsync(int trackerId)
    {
        UserStatusCollection? statusCollection;
        int removed = 0;

        foreach(var key in statuses.Keys.ToList())
        {
            if(statuses.TryGetValue(key, out statusCollection))
            {
                await statusCollection!.CollectionLock.WaitAsync();

                try
                {
                    removed += statusCollection!.Statuses.RemoveAll(x => x.trackerId == trackerId);
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