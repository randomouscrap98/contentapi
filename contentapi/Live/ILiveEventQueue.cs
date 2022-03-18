using contentapi.Search;
using contentapi.Views;

namespace contentapi.Live;

public interface ILiveEventQueue
{
    Task<object> AddEventAsync(LiveEvent data);
    Task<LiveData> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null);
    int GetCurrentLastId();
    SearchRequest GetAutoContentRequest(string query = "");
}