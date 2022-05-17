
using contentapi.Db;

namespace contentapi.data.Views;

[ResultFor(RequestType.vote)]
[SelectFrom("content_votes")]
[WriteAs(typeof(Db.ContentVote))]
public class VoteView : IContentUserRelatedView
{
    [FieldSelect]
    public long id { get; set; }

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    [FieldSelect]
    [Writable]
    public VoteType vote { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }
}