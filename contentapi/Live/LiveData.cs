using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Live;


public class LiveData
{
    public int lastId {get;set;} = 0;
    public List<EventDataView> events {get;set;} = new List<EventDataView>();
    public Dictionary<EventType, Dictionary<string, QueryResultSet>> data {get;set;} = new Dictionary<EventType, Dictionary<string, QueryResultSet>>();
}