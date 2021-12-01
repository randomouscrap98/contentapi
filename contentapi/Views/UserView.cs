using contentapi.Search;

namespace contentapi.Views;

[FromRequest(RequestType.user)]
public class UserView : AgentView
{
    [Searchable]
    public bool super {get;set;}

    [Searchable]
    [FromField("")] //Not a field you can select
    public bool registered {get;set;}

    [FromField("")]
    public List<long> groups {get;set;} = new List<long>();
}