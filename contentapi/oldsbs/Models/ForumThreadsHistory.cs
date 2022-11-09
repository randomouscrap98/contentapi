namespace contentapi.oldsbs;

public class ForumThreadsHistory : ForumThreads
{
    public string action {get;set;} = "";
    public long revision {get;set;}
    public DateTime revisiondate {get;set;}
}