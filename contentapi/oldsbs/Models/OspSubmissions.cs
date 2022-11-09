namespace contentapi.oldsbs;

public class OspSubmission
{
    public long osid {get;set;} // primary key
    public long ogid {get;set;} // points to OspGroup!!
    public long uid {get;set;}
    public DateTime createdon {get;set;}
    public string codeimage {get;set;} = "";
    public string runimage {get;set;} = "";
    public string description {get;set;} = "";
    public string initialkey {get;set;} = "";
    public string filename {get;set;} = "";
}