namespace contentapi.oldsbs;

public class OspContest
{
    public long ogid {get;set;} //primary key?? what is group for??
    public bool isopen {get;set;}
    public DateTime endon {get;set;}
    public string link {get;set;} = "";
    public long uid {get;set;}
}