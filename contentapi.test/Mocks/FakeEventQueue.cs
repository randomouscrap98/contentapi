using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Live;

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
}