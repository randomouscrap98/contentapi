namespace contentapi.Live;

public class AnnotatedCacheItem
{
    //Some tracking junk
    public static int nextId = 0;
    public int id {get;set;} = Interlocked.Increment(ref nextId);
    public DateTime createDate = DateTime.UtcNow;

    //The actual data
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();
    public object data {get;set;} = false;
}