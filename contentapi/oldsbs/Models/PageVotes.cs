namespace contentapi.oldsbs;

public class PageVotes
{
    public long pid {get;set;} // the page
    public long uid {get;set;} // the vote (primary keys with pid)
    public int vote {get;set;} //plus or minus i think?
}