using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Live;

//This is the event along with associated data pre-retrieved from the database in order to enable an optimized 
//route for live updates. in this way, the last couple of events have cached data associated with them, so when
//live listeners ask for the latest event, they don't have to look up the database data. Note that the 
//permissions for the event are associated with the event itself, and not with the cached data.
public class LiveEventCachedData
{
    //Because eventdata is a linkedcheckpointid, we must always defer to them for the true ID for any event.
    //We cannot know the id at any point, and thus must rely on the event data for the id.
    public LiveEvent evnt = new LiveEvent();
    public DateTime createDate = DateTime.UtcNow;

    public Dictionary<string, QueryResultSet>? data {get;set;}
}