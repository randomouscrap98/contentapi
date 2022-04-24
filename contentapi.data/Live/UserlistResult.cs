namespace contentapi.data;

public class UserlistResult
{
    public Dictionary<long, Dictionary<long, string>> statuses = new Dictionary<long, Dictionary<long, string>>();
    public Dictionary<string, IEnumerable<IDictionary<string, object>>> objects = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
}
