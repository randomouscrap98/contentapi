using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Live;


public class LiveData
{
    public bool optimized {get;set;} = false;
    public int lastId {get;set;} = 0;
    public List<LiveEventView> events {get;set;} = new List<LiveEventView>();
    public Dictionary<EventType, Dictionary<string, QueryResultSet>> objects {get;set;} = new Dictionary<EventType, Dictionary<string, QueryResultSet>>();
}