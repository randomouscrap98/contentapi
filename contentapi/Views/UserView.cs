namespace contentapi.Views;

public class UserView
{
    [Searchable]
    public long id {get;set;}

    [Searchable]
    public string username {get;set;} = "";

    [Searchable]
    public long avatar {get;set;}

    public string? special {get;set;}

    [Searchable]
    public DateTime createDate {get;set;}

    [Searchable]
    public bool registered {get;set;}
}