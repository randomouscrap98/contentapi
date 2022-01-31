
using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.ContentWatch))]
[ForRequest(RequestType.watch)]
public class WatchView : IIdView
{
    [Searchable]
    public long id { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.None)]
    public long contentId { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.AutoUserId)] //Needs to be the user that sent the watchId
    public long userId { get; set; }

    [Searchable]
    public long lastCommentId { get; set; }

    [Searchable]
    public long lastActivityId { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.AutoDate)]
    public DateTime createDate { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoDate)]
    public DateTime editDate { get; set; }
}