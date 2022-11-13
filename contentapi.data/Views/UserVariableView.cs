
namespace contentapi.data.Views;

[ResultFor(RequestType.uservariable)]
[SelectFrom("user_variables")]
[WriteAs(typeof(Db.UserVariable))]
public class UserVariableView : IIdView
{
    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id {get;set;}

    [DbField]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId {get;set;}

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate { get; set; }

    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.Increment)]
    public int editCount { get; set; }

    [DbField]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public string key {get;set;} = "";

    [Multiline]
    [DbField]
    [Writable]
    public string value {get;set;} = "";
}