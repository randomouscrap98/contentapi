using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Live;
using contentapi.data.Views;
using contentapi.data;

namespace contentapi.test.Mock;

public class FakeEventQueue : ILiveEventQueue
{
    public List<LiveEvent> Events = new List<LiveEvent>();
    public object ReturnData = false;

    public int QueueSize => throw new System.NotImplementedException();

    public Task<object> AddEventAsync(LiveEvent data)
    {
        Events.Add(data);
        return Task.FromResult(ReturnData);
    }

    public SearchRequest GetAutoContentRequest(string query = "", string name = "")
    {
        throw new System.NotImplementedException();
    }

    public int GetCurrentLastId()
    {
        throw new System.NotImplementedException();
    }

    public Task<LiveData> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null)
    {
        throw new System.NotImplementedException();
    }
}