using contentapi.data;

namespace contentapi.Live;

/// <summary>
/// Represents the statuses for a single room, or just any group of statuses that belong together.
/// Use the lock to control access to the list
/// </summary>
public class UserStatusCollection
{
    public List<UserStatus> Statuses {get;} = new List<UserStatus>();
    public SemaphoreSlim CollectionLock {get;} = new SemaphoreSlim(1,1);
}