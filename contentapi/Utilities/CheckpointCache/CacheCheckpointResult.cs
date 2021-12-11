namespace contentapi.Utilities;

public class CacheCheckpointResult<T>
{
    public int LastId {get;set;}
    public List<T> Data {get;set;} = new List<T>();
}
