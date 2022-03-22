using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.uservariable)]
[SelectFrom("user_variables")]
[WriteAs(typeof(Db.UserVariable))]
public class UserVariableView : IIdView
{
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate { get; set; }

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Increment)]
    public int editCount { get; set; }

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public string key {get;set;} = "";

    [Multiline]
    [FieldSelect]
    [Writable]
    public string value {get;set;} = "";
}