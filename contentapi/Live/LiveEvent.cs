using contentapi.Db;
using contentapi.Utilities;
using Newtonsoft.Json;

namespace contentapi.Live;

// 44 + 24 = 68 bytes per item
// 100k items = 6.8mb. Totally fine.
//An event
public class LiveEvent : ILinkedCheckpointId
{
    //Because it's ILinkedCheckpointId, id automatically assigned when added to checkpoint cache
    public int id {get;set;} //= Interlocked.Increment(ref nextId);   // 4 bytes
    public DateTime date {get;set;} = DateTime.UtcNow;              // 8
    public long userId {get;set;}                                   // 8 
    public UserAction action {get;set;}                             // 8
    public EventType type {get;set;}                              // 8
    public long refId {get;set;}                                    // 8

    //managed by the internal system.
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();

    public LiveEvent() { }

    public LiveEvent(long userId, UserAction action, EventType type, long refId)
    {
        this.userId = userId;
        this.action = action;
        this.type = type;
        this.refId = refId;
    }
}