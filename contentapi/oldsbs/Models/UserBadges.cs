namespace contentapi.oldsbs;

//Don't know where to store these yet. If badges are files with
//special values... how to associate a file with a user? Maybe
//comments on the file are who its assigned to?
public class UserBadges
{
    public long bid {get;set;}
    public long uid {get;set;}
    public DateTime received {get;set;}
    public long displayindex {get;set;}
}