using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

//select h.contentId, h.createUserId, count(h.id), min(h.id), max(h.id), min(h.createDate), max(h.createDate) from content_history h join content c on h.contentId = c.id where h.id>5000 and c.internalType<>3 group by h.contentId, h.createUserId;

[ResultFor(RequestType.activity_aggregate)]
[SelectFrom("content_history h join content c on h.contentId = c.id")]
[GroupBy("h.contentId, h.createUserId")]
[ExtraQueryFields("id", "createDate")] //This still requires a macro for "not files" though... egh
public class ActivityAggregateView
{
    [FieldSelect("h.contentId")]
    public long contentId { get; set; }

    [FieldSelect("h.createUserId")]
    public long createUserId { get; set; }


    [FieldSelect("count(h.id)")]
    public long count { get;set; }

    [NoQuery]
    [FieldSelect("max(h.id)")]
    public long maxId {get;set;}

    [NoQuery]
    [FieldSelect("min(h.id)")]
    public long minId {get;set;}

    [NoQuery]
    [FieldSelect("max(h.createDate)")]
    public DateTime maxCreateDate {get;set;}

    [NoQuery]
    [FieldSelect("min(h.createDate)")]
    public DateTime minCreateDate {get;set;}
}