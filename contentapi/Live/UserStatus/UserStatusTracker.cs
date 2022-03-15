namespace contentapi.Live;

public class UserStatusTracker : IUserStatusTracker
{
    protected ILogger logger;
    protected Dictionary<long, List<UserStatus>> statuses = new Dictionary<long, List<UserStatus>>();
    protected readonly SemaphoreSlim statusLock = new SemaphoreSlim(1, 1);

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

        await statusLock.WaitAsync();

        try
        {
            if(!statuses.ContainsKey(contentId))
                statuses.Add(contentId, new List<UserStatus>());
            
            //Need to remove any old statuses only for OUR tracker
            statuses[contentId].RemoveAll(x => x.userId == userId && x.trackerId == trackerId);
            statuses[contentId].Add(userStatus); //Always adds to end
        }
        finally
        {
            statusLock.Release();
        }
    }

    public Dictionary<long, Dictionary<long, string>> GetAllStatuses()
    {
        throw new NotImplementedException();
    }

    public Dictionary<long, string> GetStatusForContent(long contentId)
    {
        throw new NotImplementedException();
    }

    public Task RemoveStatusesByTracker(int trackerId)
    {
        throw new NotImplementedException();
    }
}