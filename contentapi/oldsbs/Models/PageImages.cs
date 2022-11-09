namespace contentapi.oldsbs;

public class PageImages
{
    public long iid {get;set;} // primary key
    public long pid {get;set;} // page ofc
    public long uid {get;set;}
    public DateTime created {get;set;}
    public string link {get;set;} = "";
    public long number {get;set;} // ??? The... ordering? yes
}