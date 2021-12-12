using contentapi.Db;
using contentapi.Search;

namespace contentapi.Live;

// 44 + 24 = 68 bytes per item
// 100k items = 6.8mb. Totally fine.
public class EventData
{
    public static int nextId = 1;

    public int id {get;set;} = Interlocked.Increment(ref nextId);   // 4 bytes
    public DateTime date {get;set;} = DateTime.UtcNow;              // 8
    public long userId {get;set;}                                   // 8 
    public UserAction action {get;set;}                             // 8
    public RequestType type {get;set;}                              // 8
    public long refId {get;set;}                                    // 8

    public EventData() { }

    public EventData(long userId, UserAction action, RequestType type, long refId)
    {
        this.userId = userId;
        this.action = action;
        this.type = type;
        this.refId = refId;
    }
}