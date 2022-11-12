namespace contentapi.oldsbs;

public class ForumPosts
{
    public long fpid {get;set;} // primary key
    public long ftid {get;set;} 
    public long uid {get;set;}
    public long euid {get;set;}
    public string content {get;set;} = "";
    public DateTime created {get;set;}
    public DateTime edited {get;set;} //Seems that all non-edited posts have this set to the same value as created
    public long status {get;set;} //literally all 0
}