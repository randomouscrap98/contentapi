
namespace contentapi.data.Views;

[ResultFor(RequestType.watch)]
[SelectFrom("content_watches as main")]
[WriteAs(typeof(Db.ContentWatch))]
public class WatchView : IContentUserRelatedView
{
    public const string MessageTable = "messages";

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id { get; set; }

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    [FieldSelect]
    [Writable]
    public long lastCommentId { get; set; }

    [FieldSelect]
    [Writable]
    public long lastActivityId { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime editDate { get; set; }

    [Expensive(2)]
    [FieldSelect("select count(*) from " + MessageTable + " c where c.contentId = main.contentId and c.id > main.lastCommentId")]
    public int commentNotifications {get;set;}

    [Expensive(2)]
    [FieldSelect("select count(*) from content_history h where h.contentId = main.contentId and h.id > main.lastActivityId")]
    public int activityNotifications {get;set;}
}