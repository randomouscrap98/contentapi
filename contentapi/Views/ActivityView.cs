using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.ContentHistory))]
[ForRequest(RequestType.activity)]
public class ActivityView : IIdView
{
    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)] //The update doesn't matter for activity, you can leave it preserve since activity CAN'T be edited or indeed generated manually by users
    public long id { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public long contentId { get; set; }

    [Searchable]
    [FromField("createUserId")]
    [WriteRule(WriteRuleType.ReadOnly)]
    public long userId { get; set; }

    [Searchable]
    [FromField("createDate")]
    [WriteRule(WriteRuleType.ReadOnly)]
    public DateTime date { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public string? message {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public UserAction action {get;set;}
}