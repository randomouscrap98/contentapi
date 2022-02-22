using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.activity_aggregate)]
[SelectFrom("content_history h join content c on h.contentId = c.id")]
[GroupBy("h.contentId, h.createUserId")]
[ExtraQueryField("id", "h.id")]
[ExtraQueryField("createDate", "h.createDate")]
[ExtraQueryField("internalType", "c.internalType")]
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