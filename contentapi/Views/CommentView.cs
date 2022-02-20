using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.comment)]
[SelectFrom("comments")]
[Where("module IS NULL")]
[WriteAs(typeof(Db.Comment))]
public class CommentView : IIdView
{
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id {get;set;}

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate {get;set;}

    [Multiline]
    [FieldSelect]
    [Writable]
    public string text {get;set;} = "";

    [FieldSelect]
    [Writable]
    public string? metadata {get;set;}

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate {get;set;}

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoUserId)]
    public long? editUserId {get;set;}

    [FieldSelect]
    //Note: completely not writable
    public bool deleted {get;set;}
}