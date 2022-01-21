using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Live;

public class EventCacheData
{
    //Because eventdata is a linkedcheckpointid, we must always defer to them for the true ID for any event.
    //We cannot know the id at any point, and thus must rely on the event data for the id.
    public EventData evnt = new EventData();
    public DateTime createDate = DateTime.UtcNow;

    public Dictionary<string, QueryResultSet>? data {get;set;}
}