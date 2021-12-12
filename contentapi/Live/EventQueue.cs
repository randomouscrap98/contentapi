using contentapi.Utilities;

namespace contentapi.Live;

public class EventQueue : IEventQueue
{
    public const string MainCheckpointName = "main";

    protected ILogger<EventQueue> logger;
    protected ICacheCheckpointTracker<EventData> eventTracker;

    public EventQueue(ILogger<EventQueue> logger, ICacheCheckpointTracker<EventData> tracker)
    {
        this.logger = logger;
        this.eventTracker = tracker;
    }

    public void AddEvent(EventData data)
    {
        eventTracker.UpdateCheckpoint(MainCheckpointName, data);
    }
}