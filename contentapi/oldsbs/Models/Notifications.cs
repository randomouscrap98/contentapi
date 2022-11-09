namespace contentapi.oldsbs;

//Will probably reset everyone's notifications so they point to the end.
//So, notifications will have to be inserted AFTER all content
public class Notifications
{
    public long nid {get;set;} // primary key
    public long uid {get;set;}
    public string area {get;set;} = "";
    public long linkid {get;set;}
    public DateTime lastcheck {get;set;} //eguh this doesn't line up with ids
}