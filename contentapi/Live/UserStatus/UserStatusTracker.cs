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

    public async Task RemoveStatusesByTrackerAsync(int trackerId)
    {
        UserStatusCollection? statusCollection;

        foreach(var key in statuses.Keys.ToList())
        {
            if(statuses.TryGetValue(key, out statusCollection))
            {
                await statusCollection!.CollectionLock.WaitAsync();

                try
                {
                    statusCollection!.Statuses.RemoveAll(x => x.trackerId == trackerId);
                }
                finally
                {
                    statusCollection!.CollectionLock.Release();
                }
            }
        }
    }
}