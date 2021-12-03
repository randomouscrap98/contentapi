namespace contentapi.Utilities;

public class CacheCheckpointResult
{
    public int LastId {get;set;}
    public List<object> Data {get;set;} = new List<object>();
}
