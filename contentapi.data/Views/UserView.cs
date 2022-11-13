using contentapi.Db;

namespace contentapi.data.Views;

[ResultFor(RequestType.user)]
[SelectFrom("users")]
[WriteAs(typeof(Db.User))]
public class UserView : IIdView
{
    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id {get;set;}

    [DbField]
    [Writable]
    public string username {get;set;} = "";

    [DbField]
    [Writable]
    public string avatar {get;set;} = "0";

    [NoQuery]
    [DbField]
    [Writable]
    public string? special {get;set;}

    [DbField]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public UserType type {get;set;}

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate {get;set;}

    [DbField]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId {get;set;}

    [DbField]
    [Writable]
    public bool super {get;set;}

    [DbField("registrationkey IS NULL")]
    public bool registered {get;set;}

    [DbField]
    public bool deleted {get;set;}

    [NoQuery]
    [Expensive(2)]
    //[Writable] //Readonly now
    public List<long> groups {get;set;} = new List<long>();

    [NoQuery]
    [Expensive(2)]
    [Writable]
    public List<long> usersInGroup {get;set;} = new List<long>();
}
