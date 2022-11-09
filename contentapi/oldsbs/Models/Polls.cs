namespace contentapi.oldsbs;

public class Polls
{
    public long pid {get;set;} // primary key
    public long uid {get;set;}
    public string title {get;set;} = "";
    public bool closed {get;set;}
    public bool hiddenresults {get;set;}
    public bool multivote {get;set;}
    public DateTime created {get;set;}
    public string link {get;set;} = "";
}