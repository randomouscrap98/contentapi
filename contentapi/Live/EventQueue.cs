using System.Collections.Concurrent;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Live;

/* --- Message to the future from 12-16-2021: ---
- The searching service "IGenericSearch" works fine, and you don't want to mess with it to make it more complicated.
  This is because there are several problems with polling on regular search results, the most important being that
  "events" such as comment edits, watch updates, etc are no longer tracked, and thus many events that the old sbs 
  system was able to report are impossible to track or search. DO NOT USE IGENERICSEARCH FOR LIVE UPDATES UNDER 
  ANY CIRCUMSTANCE!
- Any method you come up with for producing live updates, regardless of what you "want", REQUIRES some event 
  tracking, as comment edits, watch updates and deletes, user variable modifications, etc are essentially 
  impossible to track unless you just report them realtime. Reporting realtime without tracking makes 
  connection drops ALWAYS (100%) require a full page reload, as you may lose comment edits, watch updates, etc.
- This service below, the "EventQueue", was a solution to the problem. Each database modification (CUD in CRUD)
  would report an "event" describing the action performed. As currently implemented, users would not be able
  to see these events, as it would just complicate the live updates API for them. Users listening when an 
  event is reported would get an "instant" update, in that only ONE database lookup is performed for data
  associated with the event, and that data is shared in-memory with all instant listeners. Permissions lookup
  is done with an in-memory Dictionary<long, string>, and is already implemented in the permissions service.
  This way, no matter how many people are listening, there is essentially a "constant" overhead (assuming the
  database lookup far outweighs thread communication, which it does). The events are stored in-memory using
  the checkpoint cache (a combination cache and update reporter) so people reconnecting can get any events
  they missed. The cache was planned to be kept around for like a day or more, as storing 100k events takes
  very little memory. Users who reconnect would have to perform a full lookup of event data for all events
  missed, as the full data is only kept around for the last 2 or 3 events. This is to stop permission
  staleness (don't want users to be able to get newly hidden data by requesting an old event ID) and to 
  save memory. Thus, the service is optimized for the common case, live updates, and slightly slower for
  reconnects (which mostly won't all happen at the same time, except when the server goes down. The 
  slowness here is acceptable though, since it happens such a small percentage of the time) 
- Small note: because live updates will most likely be used to update activity and whatever, and NOT to 
  update the "currently viewed" page, content updates should be reported as ACTIVITY, not as the full
  content. This makes implementing frontends easier, as live updates should provide ALL the data that
  users want.
- The main problem with the below service is that it is probably doing too much work. Because it is looking up
  the data for events itself, it must have a searcher injected into it. This means it is holding onto a 
  TRANSIENT service, as anything with a database connection is transient. But, since it is a cache system,
  it must hold onto a cache that outlives the transient lifetime. This is simple to remedy, either through
  an injected cache (already being done for the checkpoint tracker) or through modifications which would
  allow the event queue to be a singleton. Since making the ConcurrentDictionary (trueCache) injected with
  a longer lifetime is consistent with other caches, this may be the best approach. However, it's troubling
  how many modifications and special rules have been made to just get this far, so I'm worried my design
  is bad. Since I can't think right now, I'm just letting it sit. But because of my bad memory system, I
  often find myself running through all the same ideas I had before, and running into all the same problems.
  Depending on how fast I go through them, I might actually end up writing an old bad system in the future
  just because I didn't think through it all again and because I'm lazy, I just deal with the consequences.
  It's a waste of time, so hopefully I'll notice this massive text. Please listen, future self.
- An alternative to this highly custom system is one where the users are forced to do most of the work. 
  This is both slower and most likely more annoying to use. It would essentially be like the old listening
  system, where a stream of "event" ids can be chained against to get the data you want. This could be built
  into the search system, so searching and listening is exactly the same. Heck, it could be a special request
  type, making the search endpoint literally the only thing anyone needs. However, this could cause the 
  slowdowns we currently have, as looking up the data you want could take 15-30ms. In the worst case, that
  means the "last" person to get updated could be waiting N*30ms, where N is the number of people in chat.
  With 15 people, that's an unacceptable 450ms. But the system would undoubtedly be simpler to 
  implement to start with...
*/  
/* --- Additional notes 1-2022 ---
- An event system that the users can query against will always remove all privacy, or require excessive 
  privacy calculations, otherwise everyone can see what everyone else is doing. As such, avoid such systems
  at all costs; always assume events should be private at all times
- Watch notifications (and other permanent alerts) will not be as granular as events. As such, don't start
  overthinking the watch system; you will simply not get permanent updates to alert counts when comments
  are edited or deleted, among other things. 
- Users of the event queue should NOT be inserting the cached data (such as the page data), because the
  event queue system ALREADY has to lookup that stuff when it is not present in the cache (for instance,
  when a user reconnects, they will most likely need things outside the super small 2-3 live cache buffer,
  and so a listen reconnect has to go get that data for itself ANYWAY, regardless of how the data was 
  provided in the first place)
*/
public class EventQueue : IEventQueue
{
    public const string MainCheckpointName = "main";
    public static readonly List<string> SimpleResultAnnotations = new List<string> { 
        RequestType.uservariable.ToString(),
        RequestType.watch.ToString()
    };

    protected ILogger<EventQueue> logger;
    protected ICacheCheckpointTracker<EventData> eventTracker;
    protected IGenericSearch search; //transient
    protected IPermissionService permissionService;

    //The cache for the few (if any) fully-pulled data for live updates. This is NOT the event queue!
    protected ConcurrentDictionary<int, AnnotatedCacheItem> trueCache;

    public EventQueue(ILogger<EventQueue> logger, ICacheCheckpointTracker<EventData> tracker, IGenericSearch search, 
        IPermissionService permissionService, ConcurrentDictionary<int, AnnotatedCacheItem> trueCache)
    {
        this.logger = logger;
        this.eventTracker = tracker;
        this.search = search;
        this.permissionService = permissionService; //TODO: MIGHT BE UNNECESSARY
        this.trueCache = trueCache; //new ConcurrentDictionary<int, AnnotatedCacheItem>();
    }

    //public delegate Dictionary<long, string> PermissionPull(Dictionary<string, IEnumerable<IDictionary<string, object>>> result);

    public async Task<object> AddEventAsync(EventData data)
    {
        //First, need to lookup the data for the event to add it to our true cache. Also need to remove old values!
        var cacheItem = await LookupInstantEventAsync(data);

        if(!trueCache.TryAdd(data.id, cacheItem))
            throw new InvalidOperationException("Somehow, adding a unique cached item to the event queue cache failed!");

        //THEN we can update the checkpoint, as that will wake up all the listeners
        eventTracker.UpdateCheckpoint(MainCheckpointName, data);

        return false;
    }

    /// <summary>
    /// Get permissions from a "normal" search result, which we expect to have just a simple single content representing the
    /// permissions for the entire thing.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public Dictionary<long, string> GetStandardContentPermissions(Dictionary<string, QueryResultSet> result)
    {
        var key = RequestType.content.ToString();

        if(!result.ContainsKey(key))
            throw new InvalidOperationException($"Tried to retrieve content permissions from 'standard' result, but result had no '{key}'");
        
        if(result[key].Count() != 1)
            throw new InvalidOperationException($"Tried to retrieve content permissions from 'standard' result, but result had multiple '{key}'");

        var content = result[key].First();
        var permKey = nameof(ContentView.permissions);

        if(!content.ContainsKey(permKey))
            throw new InvalidOperationException($"Tried to retrieve content permissions from 'standard' result, but result from '{key}' had no permissions key '{permKey}'");

        //We can be "reasonably" sure that it's a dictionary
        return (Dictionary<long, string>)content[permKey];
    }

    /// <summary>
    /// Construct the searchrequest that will obtain the desired data for the given list of events. The events must ALL be the same type, otherwise a search
    /// request can't be constructed!
    /// </summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public SearchRequests GetSearchRequestsForEvents(IEnumerable<EventData> events)
    {
        if(events.Select(x => x.type).Distinct().Count() != 1)
            throw new InvalidOperationException($"GetSearchRequestForEvents called with more or less than one event type! Events: {events.Count()}");

        var first = events.First();
        var requests = new SearchRequests()
        {
            values = new Dictionary<string, object> {
                { "ids", events.Select(x => x.refId) }
            }
        };

        var contentRequest = new Func<string, SearchRequest>(q => new SearchRequest {
            type = RequestType.content.ToString(),
            fields = "id,name,parentId,createDate,createUserId,deleted",
            query = q
        });
        var basicRequest = new Func<string, SearchRequest>(t => new SearchRequest {
            type = t,
            fields = "*",
            query = "id in @ids"
        });

        if(first.type == EventType.comment)
        {
            requests.requests.Add(basicRequest(RequestType.comment.ToString())); 
            requests.requests.Add(contentRequest("id in @comment.contentId"));
            requests.requests.Add(new SearchRequest()
            {
                type = RequestType.user.ToString(),
                fields = "*",
                query = "id in @content.createUserId or id in @comment.createUserId or id in @comment.editUserId"
            });
        }
        else if(first.type == EventType.activity)
        {
            requests.requests.Add(basicRequest(RequestType.activity.ToString())); 
            requests.requests.Add(contentRequest("id in @activity.contentId"));
            requests.requests.Add(new SearchRequest()
            {
                type = RequestType.user.ToString(),
                fields = "*",
                query = "id in @content.createUserId or id in @activity.userId"
            });
        }
        else if(first.type == EventType.user)
        {
            requests.requests.Add(basicRequest(RequestType.user.ToString())); 
        }
        else if(first.type == EventType.uservariable) 
        {
            requests.requests.Add(basicRequest(RequestType.uservariable.ToString())); 
        }
        else if(first.type == EventType.watch) 
        {
            requests.requests.Add(basicRequest(RequestType.watch.ToString())); 
        }
        else
        {
            throw new InvalidOperationException($"Can't understand event type {first.type}, event references {string.Join(",", events.Select(x => x.refId))}");
        }

        return requests;
    }

    public void AnnotateResult(Dictionary<string, QueryResultSet> result, EventData evnt)
    {
        //For all result sets in our (probably truecache item result), see if any of them require annotations.
        //If so, annotate the "action" on each of the items. I think this is because some of these database
        //results don't have a concept of an action, such as watch create/delete, etc.
        foreach(var annotate in SimpleResultAnnotations)
        {
            if (result.ContainsKey(annotate))
            {
                foreach(var item in result[annotate])
                    item.Add("action", evnt.action);
            }
        }
    }

    public async Task<AnnotatedCacheItem> LookupInstantEventAsync(EventData evnt)
    {
        var result = new AnnotatedCacheItem();

        //All of these are basically: construct a search, get data, set permissions
        var requests = GetSearchRequestsForEvents(new List<EventData> { evnt });

        var searchData = await search.SearchUnrestricted(requests);
        result.data = searchData.data;

        //Only time we need to actually annotate the permissions is... well, when we're returning annotated cache items, so just this function
        if(evnt.type == EventType.activity || evnt.type == EventType.comment)
            result.permissions =  GetStandardContentPermissions(searchData.data);
        else if(evnt.type == EventType.user)
            result.permissions = new Dictionary<long, string> { { 0, "R" }};
        else if(evnt.type == EventType.uservariable || evnt.type == EventType.watch)
            result.permissions = new Dictionary<long, string> { { evnt.userId, "R" }};
        else
            throw new InvalidOperationException($"Don't know how to compute permissions for event type {evnt.type}");
        
        //And now, the thing we do no matter what: need to modify certain types of results
        AnnotateResult(searchData.data, evnt);

        return result;
    }

    public async Task<List<object>> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null)
    {
        var cancelToken = token ?? CancellationToken.None;
        var checkpoint = await eventTracker.WaitForCheckpoint(MainCheckpointName, lastId, cancelToken);
        var events = checkpoint.Data;

        //See if all the events are in the trueCache. If so, just return, you're doonneeee
        var result = new List<AnnotatedCacheItem>();
        var unseenIds = new List<int>();

        foreach(var ev in events)
        {
            var temp = new AnnotatedCacheItem();

            if(!trueCache.TryGetValue(ev.id, out temp))
            {
                unseenIds.Add(ev.id);
                continue;
            }

            result.Add(temp);
        }

        //Oops, need to perform a lookup for each and every... thing.
        if(unseenIds.Count > 0)
        {
            throw new InvalidOperationException("No backtracking in live updates yet!");
        }

        return result.Select(x => x.data).ToList();
    }
}