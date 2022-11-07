namespace contentapi.data.Views;

[ResultFor(RequestType.keyword_aggregate)]
[SelectFrom("content_keywords")]
[GroupBy("value")]
[ExtraQueryField("contentId")]
public class KeywordAggregateView
{
    [FieldSelect]
    public string value { get; set; } = "";

    [FieldSelect("count(id)")]
    public long count { get;set; }
}