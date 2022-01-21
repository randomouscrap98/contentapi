using contentapi.Db;
using contentapi.Search;
using contentapi.Utilities;

namespace contentapi.Live;

// 44 + 24 = 68 bytes per item
// 100k items = 6.8mb. Totally fine.
public class EventData : ILinkedCheckpointId
{
    //public static int nextId = 1;

    public int id {get;set;} //= Interlocked.Increment(ref nextId);   // 4 bytes
    public DateTime date {get;set;} = DateTime.UtcNow;              // 8
    public long userId {get;set;}                                   // 8 
    public UserAction action {get;set;}                             // 8
    public EventType type {get;set;}                              // 8
    public long refId {get;set;}                                    // 8

    //managed by the internal system.
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();

    public EventData() { }

    public EventData(long userId, UserAction action, EventType type, long refId)
    {
        this.userId = userId;
        this.action = action;
        this.type = type;
        this.refId = refId;
    }
}