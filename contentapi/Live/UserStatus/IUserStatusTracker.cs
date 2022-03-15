namespace contentapi.Live;

public interface IUserStatusTracker
{
    Task AddStatusAsync(long userId, long contentId, string status, int trackerId);
    Dictionary<long, Dictionary<long, string>> GetAllStatuses();
    Task RemoveStatusesByTracker(int trackerId);
    Dictionary<long, string> GetStatusForContent(long contentId);
}