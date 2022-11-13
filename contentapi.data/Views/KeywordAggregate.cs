namespace contentapi.data.Views;

[ResultFor(RequestType.keyword_aggregate)]
[SelectFrom("content_keywords")]
[GroupBy("value")]
[ExtraQueryField("contentId")]
public class KeywordAggregateView
{
    [DbField]
    public string value { get; set; } = "";

    [DbField("count(id)")]
    public long count { get;set; }
}