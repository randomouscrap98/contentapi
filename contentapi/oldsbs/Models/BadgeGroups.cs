namespace contentapi.oldsbs;

public class BadgeGroups
{
    public long bgid {get;set;} //primary key
    public string name {get;set;} = "";
    public string? description {get;set;}
    public bool single {get;set;}
    public bool starter {get;set;}
}
