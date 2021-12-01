using contentapi.Search;

namespace contentapi.Views;

[FromDb(typeof(Db.User))]
[FromRequest(RequestType.agent)]
public class AgentView
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
}