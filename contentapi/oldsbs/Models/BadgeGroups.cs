namespace contentapi.oldsbs;

// Fully converted as system content of type "badgegroup" owned by super user
public class BadgeGroups
{
    public long bgid {get;set;} //primary key
    public string name {get;set;} = "";
    public string? description {get;set;}
    public bool single {get;set;}
    public bool starter {get;set;}
}
