namespace contentapi.oldsbs;

public class PagesHistory : Pages
{
    public string action {get;set;} = "";
    public long revision {get;set;}
    public DateTime revisiondate {get;set;}
}