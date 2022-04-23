
namespace contentapi.data.Views;

[ResultFor(RequestType.message_aggregate)]
[SelectFrom("messages")]
[GroupBy("contentId, createUserId")]
[ExtraQueryField("id")]
[ExtraQueryField("createDate")]
[ExtraQueryField("module")]
public class MessageAggregateView
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