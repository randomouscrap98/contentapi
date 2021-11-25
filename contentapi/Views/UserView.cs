using contentapi.Search;

namespace contentapi.Views;

[FromDb(typeof(Db.User))]
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
    [FromField("")] //Not a field you can select
    public bool registered {get;set;}
}