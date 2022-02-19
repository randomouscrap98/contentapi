using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.Comment))]
//[ForRequest(RequestType.comment)]
[ResultFor(RequestType.comment)]
[SelectFrom("comments")]
[Where("module IS NULL")]
[WriteAs(typeof(Db.Comment))]
public class CommentView : IIdView
{
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    //[WriteRule(WriteRuleType.None)] //The first parameter is for inserts, and the default write rule for updates (2nd param) is preserve, so...
    public long id {get;set;}

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId {get;set;}

    //[Queryable] //Maybe a problem
    //[AutoDate(true, false)]
    //[WriteRule(WriteRuleType.AutoDate)]
    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate {get;set;}

    //[Searchable] //Maybe a super bad idea
    [Multiline]
    [FieldSelect]
    [Writable]
    public string text {get;set;} = "";

    [FieldSelect]
    [Writable]
    public string? metadata {get;set;}

    //[Searchable]
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoDate)]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate {get;set;}

    //[Searchable]
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoUserId)]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoUserId)]
    public long? editUserId {get;set;}

    //[Searchable]
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.ReadOnly)]
    [FieldSelect]
    //Note: completely not writable
    public bool deleted {get;set;}
}