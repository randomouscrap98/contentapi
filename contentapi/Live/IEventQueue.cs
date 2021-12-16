namespace contentapi.Live;

public interface IEventQueue
{

    Task<object> AddEventAsync(EventData data);
}