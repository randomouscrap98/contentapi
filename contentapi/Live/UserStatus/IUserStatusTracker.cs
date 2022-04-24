namespace contentapi.Live;

public interface IUserStatusTracker
{
    /// <summary>
    /// Add a status for the given user in the given content on behalf of the given system 
    /// (represented by trackerId, probably a websocket)
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="contentId"></param>
    /// <param name="status"></param>
    /// <param name="trackerId"></param>
    /// <returns>The amount of statuses this overwrote, and whether the status was actually added</returns>
    Task<Tuple<int, bool>> AddStatusAsync(long userId, long contentId, string? status, int trackerId);

    /// <summary>
    /// Get the statuses for each user within each room. Outer dictionary key is contentId,
    /// inner dictionary are the statuses per user, key is userId
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<long, Dictionary<long, string>>> GetUserStatusesAsync(params long[] contentIds); //IEnumerable<long>? contentIds = null);

    /// <summary>
    /// Remove all statuses reported by the given tracker in all rooms
    /// </summary>
    /// <param name="trackerId"></param>
    /// <returns></returns>
    Task<Dictionary<long, int>> RemoveStatusesByTrackerAsync(int trackerId);

    /// <summary>
    /// Get the statuses for a single room as given by contentId. 
    /// </summary>
    /// <param name="contentId"></param>
    /// <returns></returns>
    //Task<Dictionary<long, string>> GetStatusForContentAsync(long contentId);
}