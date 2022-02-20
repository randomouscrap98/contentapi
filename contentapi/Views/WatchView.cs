
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.watch)]
[SelectFrom("content_watches")]
[WriteAs(typeof(Db.ContentWatch))]
public class WatchView : IIdView
{
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
}