namespace contentapi.oldsbs;

//Fully converted as just bans, lose shadow/lockout
public class Bans
{
    public long bid {get;set;} //primary key
    public long uid {get;set;}
    public DateTime end {get;set;}
    public string? reason {get;set;}
    public bool site {get;set;}
    public bool lockout {get;set;}
    public bool shadow {get;set;}
}