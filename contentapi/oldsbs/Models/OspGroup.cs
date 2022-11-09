namespace contentapi.oldsbs;

public class OspGroup
{
    public long ogid {get;set;} //primary key
    public string name {get;set;} = ""; //unique constraint, is THIS the contest??
    public DateTime createdon {get;set;}
}