using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Live;
using contentapi.Views;

namespace contentapi.test.Mock;

public class FakeEventQueue : IEventQueue
{
    public List<LiveEvent> Events = new List<LiveEvent>();
    public object ReturnData = false;

    public Task<object> AddEventAsync(LiveEvent data)
    {
        Events.Add(data);
        return Task.FromResult(ReturnData);
    }

    public Task<LiveData> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null)
    {
        throw new System.NotImplementedException();
    }
}