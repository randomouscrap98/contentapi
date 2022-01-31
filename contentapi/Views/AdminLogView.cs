
using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.AdminLog))]
[ForRequest(RequestType.adminlog)]
public class AdminLogView : IIdView
{
    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)] //The update doesn't matter for admin log, you can leave it preserve since logs CAN'T be edited or indeed generated manually by users
    public long id {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public AdminLogType type {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public string? text {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public DateTime createDate {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public long initiator {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.ReadOnly)]
    public long target {get;set;}
}