namespace contentapi.oldsbs;

//This table just logs who did what. Any log-type tables could
//probably be messages inside a special content type.
//WARN: NOT NECESSARILY ACCURATE! Remember that this table says who has what badge!!
//WARN2: AGAIN HANG ON, I think "userbadges" is the one that says who has what badge, this is just a log for real
public class GivenBadges
{
    //triple primary key, badgeid, giver, receiver
    public long bid {get;set;} 
    public long giver {get;set;}
    public long receiver {get;set;}
    public DateTime given {get;set;}
}