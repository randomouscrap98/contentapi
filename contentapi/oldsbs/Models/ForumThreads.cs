namespace contentapi.oldsbs;

public class ForumThreads
{
    public long ftid {get;set;} //primary key
    public long fcid {get;set;} //category (parent)
    public long uid {get;set;}
    public string title {get;set;} = "";
    public DateTime created {get;set;}
    public long views {get;set;}
    public long status {get;set;} //what is this??
}