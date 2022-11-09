namespace contentapi.oldsbs;

public class Pages
{
    public long pid {get;set;} // primary key
    public DateTime created {get;set;}
    public DateTime edited {get;set;}
    public long euid {get;set;}
    public string title {get;set;} = "";
    public string dlkey {get;set;} = "";
    public string version {get;set;} = "";
    public string size {get;set;} = "";
    public string body {get;set;} = "";
    public string dmca {get;set;} = "";
    public long support {get;set;}
}