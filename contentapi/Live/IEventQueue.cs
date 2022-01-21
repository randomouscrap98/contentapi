using contentapi.Views;

namespace contentapi.Live;

public interface IEventQueue
{
    Task<object> AddEventAsync(LiveEvent data);
    Task<LiveData> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null);
}