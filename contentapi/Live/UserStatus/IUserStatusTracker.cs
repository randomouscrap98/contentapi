namespace contentapi.Live;

public interface IUserStatusTracker
{
    Task AddStatusAsync(long userId, long contentId, string status, int trackerId);
    Task<Dictionary<long, Dictionary<long, string>>> GetAllStatusesAsync();
    Task RemoveStatusesByTrackerAsync(int trackerId);
    Task<Dictionary<long, string>> GetStatusForContentAsync(long contentId);
}