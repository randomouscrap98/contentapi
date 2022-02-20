using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.comment_aggregate)]
[SelectFrom("comments")]
[GroupBy("contentId, createUserId")]
[ExtraQueryFields("id")]
public class CommentAggregateView
{
    [FieldSelect]
    public long contentId { get; set; }

    [FieldSelect]
    public long createUserId { get; set; }


    [FieldSelect("count(id)")]
    public long count { get;set; }

    [NoQuery]
    [FieldSelect("max(id)")]
    public long maxId {get;set;}

    [NoQuery]
    [FieldSelect("min(id)")]
    public long minId {get;set;}

    [NoQuery]
    [FieldSelect("max(createDate)")]
    public DateTime maxCreateDate {get;set;}

    [NoQuery]
    [FieldSelect("min(createDate)")]
    public DateTime minCreateDate {get;set;}
}