
namespace contentapi.data.Views;

[ResultFor(RequestType.message_aggregate)]
[SelectFrom("messages")]
[GroupBy("contentId, createUserId")]
[ExtraQueryField("id")]
[ExtraQueryField("createDate")]
[ExtraQueryField("module")]
public class MessageAggregateView
{
    [DbField]
    public long contentId { get; set; }

    [DbField]
    public long createUserId { get; set; }


    [DbField("count(id)")]
    public long count { get;set; }

    [NoQuery]
    [DbField("max(id)")]
    public long maxId {get;set;}

    [NoQuery]
    [DbField("min(id)")]
    public long minId {get;set;}

    [NoQuery]
    [DbField("max(createDate)")]
    public DateTime maxCreateDate {get;set;}

    [NoQuery]
    [DbField("min(createDate)")]
    public DateTime minCreateDate {get;set;}
}