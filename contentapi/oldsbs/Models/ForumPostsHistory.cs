namespace contentapi.oldsbs;

public class ForumPostsHistory : ForumPosts
{
    public string action {get;set;} = "";
    public long revision {get;set;}
    public DateTime revisiondate {get;set;}
}