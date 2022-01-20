namespace contentapi.Live;

public class PermissionCacheData 
{
    public Dictionary<long, string> Permissions {get;set;} = new Dictionary<long, string>();
    public int MaxLinkId {get;set;} = 0;
}
