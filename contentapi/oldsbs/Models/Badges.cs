namespace contentapi.oldsbs;

public class Badges
{
    public long bid {get;set;} //primary key
    public string file {get;set;} = "";
    public string name {get;set;} = "";
    public string? description {get;set;}
    public int value {get;set;}
    public bool givable {get;set;}
    public bool hidden {get;set;}
    public bool single {get;set;}
}