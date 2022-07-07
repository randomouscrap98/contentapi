using AutoMapper;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Search;
using contentapi.Utilities;
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

public class LiveEventQueueConfig
{
    public TimeSpan DataCacheExpire {get;set;} = TimeSpan.FromSeconds(10);
    public int MaxEventListen {get;set;} = 1000;
}

public class LiveEventQueue : ILiveEventQueue
{
    public const string MainCheckpointName = "main";
    public static readonly List<string> SimpleResultAnnotations = new List<string> { 
        RequestType.uservariable.ToString(),
        RequestType.watch.ToString()
    };

    protected ILogger<LiveEventQueue> logger;
    protected ICacheCheckpointTracker<LiveEvent> eventTracker; //THIS is the event queue!
    protected Func<IGenericSearch> searchProducer; //A search generator to ensure this queue can be any lifetime it wants (this is an anti-pattern maybe?)
    protected IPermissionService permissionService;
    protected IMapper mapper;
    protected LiveEventQueueConfig config;

    //The cache for the few (if any) fully-pulled data for live updates. This is NOT the event queue!
    protected List<LiveEventCachedData> dataCache = new List<LiveEventCachedData>();
    protected Dictionary<long, PermissionCacheData> permissionCache = new Dictionary<long, PermissionCacheData>();
    protected readonly object dataCacheLock = new Object();
    protected readonly object permissionCacheLock = new Object();

    public LiveEventQueue(ILogger<LiveEventQueue> logger, LiveEventQueueConfig config, ICacheCheckpointTracker<LiveEvent> tracker, Func<IGenericSearch> searchProducer, 
        IPermissionService permissionService, IMapper mapper)
    {
        this.logger = logger;
        this.eventTracker = tracker;
        this.searchProducer = searchProducer;
        this.permissionService = permissionService; 
        this.config = config;
        this.mapper = mapper;
    }

    public async Task<object> AddEventAsync(LiveEvent evnt)
    {
        //First, need to lookup the data for the event to add it to our true cache. Also need to remove old values!
        var cacheItem = await LookupEventDataAsync(evnt);

        //This is the ONLY place we're performing this permission calculation nonsense. Be VERY CAREFUL, this is
        //quite the hack!
        if(evnt.type == EventType.activity_event || evnt.type == EventType.message_event)
        {
            var recipientPerms = new Dictionary<long, string>();
            var data = cacheItem.data ?? throw new InvalidOperationException("No cache result data to pull permissions from!");

            if(evnt.type == EventType.message_event)
                recipientPerms = GetRestrictedMessagePermissions(data);

            //The special case: restricted message permissions ALWAYS override others!
            if(recipientPerms.Count > 0)
            {
                evnt.permissions = recipientPerms;
            }
            else
            {
                var currentPermissions = GetStandardContentPermissions(data);

                //Go figure out our permission linking
                lock (permissionCacheLock)
                {
                    //The permissions are brand new, just add an empty object
                    if (!permissionCache.ContainsKey(currentPermissions.Item1))
                        permissionCache.Add(currentPermissions.Item1, new PermissionCacheData());

                    //We must modify the dictionary IN PLACE so we don't replace the reference! THIS IS CRITICAL TO MAKING THE PERMISSION UPDATE SYSTEM WORK!
                    var permData = permissionCache[currentPermissions.Item1];
                    permData.MaxLinkId = evnt.id;
                    permData.Permissions.Clear();

                    //The very special modification in-place. Slightly slower, but saves a LOT of complexity and processing for permission updates!
                    foreach (var kv in currentPermissions.Item2)
                        permData.Permissions.Add(kv.Key, kv.Value);

                    //This makes the event permissions reference the permissions WITHIN our tracked permission dictionary!
                    //This way, when OTHER events come in with new permissions for the same content, it will go through and
                    //update ALL prior events! Hopefully!
                    evnt.permissions = permData.Permissions;

                    //TODO: Add the old permission removal!
                }
            }

        }
        else if(evnt.type == EventType.user_event)
        {
            evnt.permissions = new Dictionary<long, string> { { 0, "R" }};
        }
        else if(evnt.type == EventType.uservariable_event || evnt.type == EventType.watch_event)
        {
            evnt.permissions = new Dictionary<long, string> { { evnt.userId, "R" }};
        }
        else
        {
            throw new InvalidOperationException($"Don't know how to compute permissions for event type {evnt.type}");
        }

        //Remove any cached items older than our timer, then add our cache item
        lock(dataCacheLock)
        {
            dataCache.RemoveAll(x => (DateTime.Now - x.createDate) > config.DataCacheExpire);
            dataCache.Add(cacheItem);
        }

        //THEN we can update the checkpoint, as that will wake up all the listeners. NOTE: sort of bad design; this has a side effect
        //of assigning the event id, since the event is an ILinkedCheckpointId. There should be a better way of doing this!
        eventTracker.UpdateCheckpoint(MainCheckpointName, evnt);

        return false;
    }

    /// <summary>
    /// Get permissions from a "normal" search result, which we expect to have just a simple single content representing the
    /// permissions for the entire thing.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public Tuple<long, Dictionary<long, string>> GetStandardContentPermissions(Dictionary<string, QueryResultSet> result)
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
        return Tuple.Create((long)content["id"], (Dictionary<long, string>)content[permKey]);
    }

    public Dictionary<long, string> GetRestrictedMessagePermissions(Dictionary<string, QueryResultSet> result)
    {
        var key = RequestType.message.ToString();

        if(!result.ContainsKey(key))
            throw new InvalidOperationException($"Tried to retrieve restricted message permissions from 'standard' result, but result had no '{key}'");
        if(result[key].Count() != 1)
            throw new InvalidOperationException($"Tried to retrieve restricted message permissions from 'standard' result, but result had multiple '{key}'");
        
        var message = result[key].First();
        var ruidKey = nameof(MessageView.receiveUserId);

        if(!message.ContainsKey(ruidKey))
            throw new InvalidOperationException($"Tried to retrieve restricted message recipient from 'standard' result, but result from '{key}' had no key '{ruidKey}'");
        
        var recipient = (long)message[ruidKey];

        //ONLY the recipient should have access to this restricted message
        if(recipient == 0)
            return new Dictionary<long, string>();
        else
            return new Dictionary<long, string>() { { recipient, "R" } }; 
    }

    public SearchRequest GetAutoContentRequest(string query, string name = "")
    {
        return new SearchRequest {
            name = name,
            type = RequestType.content.ToString(),
            fields = "id,name,parentId,lastRevisionId,createDate,createUserId,deleted,permissions,contentType,literalType,hash,values",
            query = query
        };
    }

    /// <summary>
    /// Construct the searchrequest that will obtain the desired data for the given list of events. The events must ALL be the same type, otherwise a search
    /// request can't be constructed!
    /// </summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public SearchRequests GetSearchRequestsForEvents(IEnumerable<LiveEvent> events)
    {
        if(events.Select(x => x.type).Distinct().Count() != 1)
            throw new InvalidOperationException($"GetSearchRequestForEvents called with more or less than one event type! Events: {events.Count()}");

        var first = events.First();
        var requests = new SearchRequests()
        {
            values = new Dictionary<string, object> {
                { "ids", events.Select(x => x.refId) },
                { "contentIds", events.Select(x => x.contentId).Where(x => x > 0) }
            }
        };

        var basicRequest = new Func<string, SearchRequest>(t => new SearchRequest {
            type = t,
            fields = "*",
            query = "id in @ids"
        });

        if(first.type == EventType.message_event)
        {
            requests.requests.Add(basicRequest(RequestType.message.ToString())); 
            requests.requests.Add(GetAutoContentRequest("id in @message.contentId"));
            requests.requests.Add(new SearchRequest()
            {
                type = RequestType.user.ToString(),
                fields = "*",
                query = "id in @content.createUserId or id in @message.createUserId or id in @message.editUserId or id in @message.uidsInText"
            });
        }
        else if(first.type == EventType.activity_event)
        {
            requests.requests.Add(basicRequest(RequestType.activity.ToString())); 
            requests.requests.Add(GetAutoContentRequest("id in @activity.contentId"));
            requests.requests.Add(GetAutoContentRequest("id in @content.parentId", "parent"));
            requests.requests.Add(new SearchRequest()
            {
                type = RequestType.user.ToString(),
                fields = "*",
                query = "id in @content.createUserId or id in @activity.userId"
            });
        }
        else if(first.type == EventType.user_event)
        {
            requests.requests.Add(basicRequest(RequestType.user.ToString())); 
        }
        else if(first.type == EventType.uservariable_event) 
        {
            requests.requests.Add(basicRequest(RequestType.uservariable.ToString())); 
        }
        else if(first.type == EventType.watch_event) 
        {
            requests.requests.Add(basicRequest(RequestType.watch.ToString())); 
            requests.requests.Add(GetAutoContentRequest("id in @watch.contentId"));
        }
        else
        {
            throw new InvalidOperationException($"Can't understand event type {first.type}, event references {string.Join(",", events.Select(x => x.refId))}");
        }

        //Also, add request for related content
        requests.requests.Add(GetAutoContentRequest("id in @contentIds", "related_content"));

        return requests;
    }

    public async Task<LiveEventCachedData> LookupEventDataAsync(LiveEvent evnt)
    {
        var requests = GetSearchRequestsForEvents(new List<LiveEvent> { evnt });
        var search = searchProducer();

        var searchData = await search.SearchUnrestricted(requests);
        return new LiveEventCachedData() { evnt = evnt, data = searchData.objects };
    }

    public async Task<CacheCheckpointResult<LiveEvent>> ListenEventsAsync(int lastId = -1, CancellationToken? token = null)
    {
        var cancelToken = token ?? CancellationToken.None;
        var checkpoint = await eventTracker.WaitForCheckpoint(MainCheckpointName, lastId, cancelToken);

        //Should this check be here? Probably... it's part of listening; our system SHOULDN'T return an
        //empty set ever, but hey who knows...
        if(checkpoint.Data.Count == 0)
            throw new InvalidOperationException("After waiting for checkpoint, received NO event data!");

        return checkpoint;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// Yes, this function requires a full UserView to listen. We need to know what groups and such the user
    /// is in for listening; other functions might look this up automatically but because this is a high traffic
    /// endpoint, we can't keep looking up the user for the caller.
    /// </remarks
    /// <param name="listener"></param>
    /// <param name="lastId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<LiveData> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null)
    {
        //Use only ONE searcher for each listen call! Hopefully this isn't a problem!
        var search = searchProducer();
        var result = new LiveData() { lastId = lastId }; 

        //No tail recursion optimization, don't do recursive call to self! Easier to just do loop anyway!
        while(true)
        {
            var checkpoint = await ListenEventsAsync(result.lastId, token);

            //Always keep our "lastId" up to date!
            result.lastId = checkpoint.LastId; //Data.Max(x => x.id);

            //Thanks to us caching permissions with the events, we can filter out the events we're not allowed to see immediately, saving us from
            //weird situations where we get a data cache miss simply due to too many events going through the system that we're not even allowed to see.
            var events = checkpoint.Data.Where(x => permissionService.CanUserStatic(listener, UserAction.read, x.permissions)); 

            if(events.Count() == 0)
                continue;  //There is NOTHING to do for this run, because the event(s) in question don't pertain to us
            else if(events.Count() > config.MaxEventListen)
                events = events.OrderBy(x => x.id).Take(config.MaxEventListen); //Don't let the user get too many events all at once!
            else if(events.Count(x => x.id == 0) > 0) //this adds to the computation but shouldn't take too long to compute...
                throw new InvalidOperationException("SAFETY CHECK FAILED: events with zero ID discovered after listen!");

            var optimalRoute = false;

            //Go ahead and set up the return events, we know we'll return SOMETHING this loop, since there's a non-zero amount
            result.events = events.Select(x => mapper.Map<LiveEventView>(x)).OrderBy(x => x.id).ToList();

            //The "fast optimized" route. Hopefully, MOST live updates go through this.
            if (events.Count() == 1)
            {
                var optimalEvent = events.First();

                lock(dataCacheLock)
                {
                    var matching = dataCache.FirstOrDefault(x => x.evnt.id == optimalEvent.id);
                    if(matching != null)
                    {
                        result.objects.Add(optimalEvent.type, matching.data ?? throw new InvalidOperationException($"No data set for event cache item {optimalEvent.id}"));
                        result.optimized = true;
                        optimalRoute = true;
                    }
                    else
                    {
                        throw new InvalidOperationException($"OPTIMAL EVENT BUT NO MATCHING: {optimalEvent.id} VS: {string.Join(",", dataCache.Select(x => x.evnt.id))}");
                    }
                }
            }

            //Oh, we weren't able to be cool and optimal. Go look up stuff manually!
            if(!optimalRoute)
            {
                foreach(var type in events.Select(x => x.type).Distinct())
                {
                    var requests = GetSearchRequestsForEvents(events.Where(x => x.type == type));
                    var searchData = await search.Search(requests, listener.id);
                    result.objects.Add(type, searchData.objects);
                }
            }

            return result;
        }
    }

    public int GetCurrentLastId()
    {
        return eventTracker.MaximumCacheCheckpoint(MainCheckpointName);
    }

    public int QueueSize => eventTracker.CacheCount;
}