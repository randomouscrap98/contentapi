namespace contentapi.oldsbs;

public class Categories
{
    public long cid {get;set;} //primary key
    public long? pcid {get;set;} //category parent (hierarchy)
    public string name {get;set;} = "";
    public string? description {get;set;}
    public long permissions {get;set;}
    public bool alwaysavailable {get;set;}
}