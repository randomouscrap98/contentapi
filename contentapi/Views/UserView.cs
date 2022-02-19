using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.User))]
//[ForRequest(RequestType.user)]
[ResultFor(RequestType.user)]
[SelectFrom("users")]
public class UserView : IIdView
{
    //[Searchable]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id {get;set;}

    //[Searchable]
    [FieldSelect]
    [Writable]
    public string username {get;set;} = "";

    //[Searchable]
    [FieldSelect]
    [Writable]
    public string avatar {get;set;} = "0";

    [NoQuery]
    [FieldSelect]
    [Writable]
    public string? special {get;set;}

    //[Searchable]
    //[WriteRule(WriteRuleType.None)]
    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public UserType type {get;set;}

    //[Searchable]
    //[WriteRule(WriteRuleType.AutoDate)]
    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate {get;set;}

    //[Searchable]
    [FieldSelect]
    [Writable]
    public bool super {get;set;}

    //[Searchable]
    //WARN: computed REMOVES this field from the query builder!
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.ReadOnly)]
    [FieldSelect]
    public bool registered {get;set;}

    //[Searchable]
    //WARN: computed REMOVES this field from the query builder!
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.ReadOnly)]
    [FieldSelect]
    public bool deleted {get;set;}

    //[FromField("")]
    //[Computed]
    [Expensive(2)]
    [Writable]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public List<long> groups {get;set;} = new List<long>();
}
