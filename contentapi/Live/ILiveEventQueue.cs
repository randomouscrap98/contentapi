using contentapi.data.Views;
using contentapi.data;

namespace contentapi.Live;

public interface ILiveEventQueue
{
    Task<object> AddEventAsync(LiveEvent data);
    Task<LiveData> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null);
    int GetCurrentLastId();
    int QueueSize {get;}
    SearchRequest GetAutoContentRequest(string query = "", string name = "");
}