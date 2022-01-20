using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Live;

public class EventCacheData
{
    //Some tracking junk
    public static int nextId = 0;
    public int id {get;set;} = Interlocked.Increment(ref nextId);
    public DateTime createDate = DateTime.UtcNow;

    public Dictionary<string, QueryResultSet>? data {get;set;}
}