namespace contentapi.oldsbs;

public class ForumFlags
{
    //just a link between a post and a user, each can only flag once
    public long fpid {get;set;} 
    public long uid {get;set;}
}