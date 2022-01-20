using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Live;
using contentapi.Views;

namespace contentapi.test.Mock;

public class FakeEventQueue : IEventQueue
{
    public List<EventData> Events = new List<EventData>();
    public object ReturnData = false;

    public Task<object> AddEventAsync(EventData data)
    {
        Events.Add(data);
        return Task.FromResult(ReturnData);
    }

    public Task<LiveData> ListenAsync(UserView listener, int lastId = -1, CancellationToken? token = null)
    {
        throw new System.NotImplementedException();
    }
}