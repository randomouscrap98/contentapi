using System.Collections.Concurrent;
using contentapi.Utilities;
using contentapi.Views;

namespace contentapi.Live;

public class EventQueue : IEventQueue
{
    public const string MainCheckpointName = "main";

    protected ILogger<EventQueue> logger;
    protected ICacheCheckpointTracker<EventData> eventTracker;
    protected ConcurrentDictionary<int, object> trueCache;

    public EventQueue(ILogger<EventQueue> logger, ICacheCheckpointTracker<EventData> tracker)
    {
        this.logger = logger;
        this.eventTracker = tracker;
        this.trueCache = new ConcurrentDictionary<int, object>();
    }

    public async Task<object> AddEventAsync(EventData data)
    {
        //First, need to lookup the data for the event to add it to our true cache. Also need to remove old values!

        //THEN we can update the checkpoint, as that will wake up all the listeners
        eventTracker.UpdateCheckpoint(MainCheckpointName, data);

        return false;
    }

    //WILL need some kind of way to compute the permissions for each type of item. Will any of them be hard types?
    public List<object> GetAllowed(UserView requester, List<object> values)
    {
        return values;
    }

    public async Task<List<object>> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null)
    {
        var cancelToken = token ?? CancellationToken.None;
        var checkpoint = await eventTracker.WaitForCheckpoint(MainCheckpointName, lastId, cancelToken);
        var events = checkpoint.Data;

        //See if all the events are in the trueCache. If so, just return, you're doonneeee
        var result = new List<object>();
        var unseenIds = new List<int>();

        foreach(var ev in events)
        {
            var temp = new object();

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

        return result;
    }
}