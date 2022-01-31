using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.User))]
[ForRequest(RequestType.user)]
public class UserView : IIdView
{
    [Searchable]
    public long id {get;set;}

    [Searchable]
    public string username {get;set;} = "";

    [Searchable]
    public string avatar {get;set;} = "0";

    public string? special {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.None)]
    public UserType type {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.AutoDate)]
    public DateTime createDate {get;set;}

    [Searchable]
    public bool super {get;set;}

    [Searchable]
    [FromField("")] //Not a field you can select
    [WriteRule(WriteRuleType.DefaultValue, WriteRuleType.Preserve)]
    public bool registered {get;set;}

    [FromField("")]
    [Computed]
    [Expensive(2)]
    public List<long> groups {get;set;} = new List<long>();
}
