namespace contentapi.oldsbs;

public class Events
{
    public long eid {get;set;} //primary key
    public long uid {get;set;}
    public DateTime time {get;set;}
    public string link {get;set;} = "";
    public string action {get;set;} = "";
    public string area {get;set;} = "";
    public string title {get;set;} = "";
    public string description {get;set;} = "";
    public bool hidden {get;set;}
    public string extra {get;set;} = "";
    public long linkid {get;set;}
}