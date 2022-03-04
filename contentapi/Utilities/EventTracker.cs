using System.Collections.Concurrent;

namespace contentapi.Utilities;

public class EventTrackerConfig
{
    public TimeSpan MaximumKeep {get;set;} = TimeSpan.FromDays(1);
}

public class EventTracker : IEventTracker
{
    protected ILogger logger;
    protected ConcurrentDictionary<string, ConcurrentQueue<DateTime>> events = new ConcurrentDictionary<string, ConcurrentQueue<DateTime>>();
    protected EventTrackerConfig config;

    public EventTracker(ILogger<EventTracker> logger, EventTrackerConfig config)
    {
        this.logger = logger;
        this.config = config;
    }

    public void AddEvent(string eventKey)
    {
        var now = DateTime.Now;
        var queue = events.GetOrAdd(eventKey, new ConcurrentQueue<DateTime>());
        queue.Enqueue(now);

        DateTime firstEvent = now;

        //It doesn't matter too much if trypeek fails, we'll get 'em next time!
        while(queue.TryPeek(out firstEvent))
        {
            //And here, to prevent what could be an infinite loop, if trydequeue fails,
            //just exit outright.
            if(now - firstEvent >= config.MaximumKeep)
                if(queue.TryDequeue(out firstEvent))
                    continue;
            
            //This happens if the first item isn't old OR we can't dequeue.
            break;
        }
    }

    public int CountEvents(string eventKey, TimeSpan time)
    {
        var past = DateTime.Now - time;
        var queue = events.GetOrAdd(eventKey, new ConcurrentQueue<DateTime>());
        return queue.Count(x => x > past);
    }
}