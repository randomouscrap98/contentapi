
using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.ContentWatch))]
//[ForRequest(RequestType.watch)]
[ResultFor(RequestType.watch)]
[SelectFrom("content_watches")]
[WriteAs(typeof(Db.ContentWatch))]
public class WatchView : IIdView
{
    //[Searchable]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.None)]
    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.AutoUserId)] //Needs to be the user that sent the watchId
    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    //[Searchable]
    [FieldSelect]
    [Writable]
    public long lastCommentId { get; set; }

    //[Searchable]
    [FieldSelect]
    [Writable]
    public long lastActivityId { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.AutoDate)]
    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoDate)]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime editDate { get; set; }
}