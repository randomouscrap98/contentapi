using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Comment))]
[ForRequest(RequestType.comment)]
public class CommentView : IIdView
{
    [Searchable]
    //[WriteRule(WriteRuleType.None)] //The first parameter is for inserts, and the default write rule for updates (2nd param) is preserve, so...
    public long id {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.None)]
    public long contentId {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.AutoUserId)]
    public long createUserId {get;set;}

    [Searchable] //Maybe a problem
    //[AutoDate(true, false)]
    [WriteRule(WriteRuleType.AutoDate)]
    public DateTime createDate {get;set;}

    [Searchable] //Maybe a super bad idea
    public string text {get;set;} = "";

    [Searchable]
    [WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoDate)]
    public DateTime? editDate {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoUserId)]
    public long? editUserId {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.DefaultValue, WriteRuleType.ReadOnly)]
    public bool deleted {get;set;}
}