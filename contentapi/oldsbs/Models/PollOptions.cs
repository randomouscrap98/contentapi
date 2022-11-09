namespace contentapi.oldsbs;

public class PollOptions
{
    public long poid {get;set;} // primary key
    public long pid {get;set;} //the POLL id (parent)
    public string content {get;set;} = ""; //the option
}