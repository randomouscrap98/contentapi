
namespace contentapi.data.Views;

[ResultFor(RequestType.watch)]
[SelectFrom("content_watches as main")]
[WriteAs(typeof(Db.ContentWatch))]
public class WatchView : IContentUserRelatedView
{
    public const string MessageTable = "messages";

    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id { get; set; }

    [DbField]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId { get; set; }

    [DbField]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    [DbField]
    [Writable]
    public long lastCommentId { get; set; }

    [DbField]
    [Writable]
    public long lastActivityId { get; set; }

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate { get; set; }

    [Expensive(2)]
    [DbField("select count(*) from " + MessageTable + " c where c.contentId = main.contentId and c.id > main.lastCommentId")]
    public int commentNotifications {get;set;}

    [Expensive(2)]
    [DbField("select count(*) from content_history h where h.contentId = main.contentId and h.id > main.lastActivityId")]
    public int activityNotifications {get;set;}
}