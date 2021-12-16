using System.Collections.Concurrent;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;

namespace contentapi.Live;

public class EventQueue : IEventQueue
{
    public const string MainCheckpointName = "main";

    protected ILogger<EventQueue> logger;
    protected ICacheCheckpointTracker<EventData> eventTracker;
    protected IGenericSearch search;
    protected IPermissionService permissionService;


    protected ConcurrentDictionary<int, AnnotatedCacheItem> trueCache;


    public EventQueue(ILogger<EventQueue> logger, ICacheCheckpointTracker<EventData> tracker, IGenericSearch search, 
        IPermissionService permissionService)
    {
        this.logger = logger;
        this.eventTracker = tracker;
        this.trueCache = new ConcurrentDictionary<int, AnnotatedCacheItem>();
        this.search = search;
        this.permissionService = permissionService; //TODO: MIGHT BE UNNECESSARY
    }

    public async Task<object> AddEventAsync(EventData data)
    {
        //First, need to lookup the data for the event to add it to our true cache. Also need to remove old values!
        var cacheItem = await LookupEventAsync(data);

        if(!trueCache.TryAdd(data.id, cacheItem))
            throw new InvalidOperationException("Somehow, adding a unique cached item to the event queue cache failed!");

        //THEN we can update the checkpoint, as that will wake up all the listeners
        eventTracker.UpdateCheckpoint(MainCheckpointName, data);

        return false;
    }

    public Dictionary<long, string> StandardContentPermissions(Dictionary<string, IEnumerable<IDictionary<string, object>>> result)
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

    public Tuple<SearchRequests, Func<Dictionary<string, IEnumerable<IDictionary<string, object>>>, Dictionary<long, string>>> GetSearchRequestForEvents(IEnumerable<EventData> events)
    {
        if(events.Select(x => x.type).Distinct().Count() != 1)
            throw new InvalidOperationException($"GetSearchRequestForEvents called with more or less than one event type! Events: {events.Count()}");

        var first = events.First();
        var requests = new SearchRequests();

        if(first.type == RequestType.comment)
        {
            return Tuple.Create(requests, StandardContentPermissions);
        }
        else
        {
            throw new InvalidOperationException($"Can't understand event type {first.type}, event references {string.Join(",", events.Select(x => x.refId))}");
        }
    }

    public async Task<AnnotatedCacheItem> LookupEventAsync(EventData evnt) //, bool includePermissions = true)
    {
        var result = new AnnotatedCacheItem();

        //if(events.Count() == 0)
        //    return result;

        //var first = events.First();

        //All of these are basically: construct a search, get data, set permissions
        var presearch = GetSearchRequestForEvents(new List<EventData> { evnt });

        var searchData = await search.SearchUnrestricted(presearch.Item1);
        result.data = searchData.data;
        result.permissions = presearch.Item2(searchData.data);

        return result;
    }

    //WILL need some kind of way to compute the permissions for each type of item. Will any of them be hard types?
    //public List<object> GetAllowed(UserView requester, List<object> values)
    //{
    //    return values;
    //}

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