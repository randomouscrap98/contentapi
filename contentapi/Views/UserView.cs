using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.user)]
[SelectFrom("users")]
[WriteAs(typeof(Db.User))]
public class UserView : IIdView
{
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id {get;set;}

    [FieldSelect]
    [Writable]
    public string username {get;set;} = "";

    [FieldSelect]
    [Writable]
    public string avatar {get;set;} = "0";

    [NoQuery]
    [FieldSelect]
    [Writable]
    public string? special {get;set;}

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public UserType type {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate {get;set;}

    [FieldSelect]
    [Writable]
    public bool super {get;set;}

    [FieldSelect("registrationkey IS NULL")]
    public bool registered {get;set;}

    [FieldSelect]
    public bool deleted {get;set;}

    [NoQuery]
    [Expensive(2)]
    [Writable]
    public List<long> groups {get;set;} = new List<long>();
}
