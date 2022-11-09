namespace contentapi.oldsbs;

//This table just logs who did what. Any log-type tables could
//probably be messages inside a special content type.
public class GivenBadges
{
    //triple primary key, badgeid, giver, receiver
    public long bid {get;set;} 
    public long giver {get;set;}
    public long receiver {get;set;}
    public DateTime given {get;set;}
}