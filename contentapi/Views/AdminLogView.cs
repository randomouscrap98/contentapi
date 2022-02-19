
using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.AdminLog))]
//[ForRequest(RequestType.adminlog)]
[ResultFor(RequestType.adminlog)]
[SelectFrom("admin_log")]
public class AdminLogView : IIdView
{
    [FieldSelect]
    public long id {get;set;}

    [FieldSelect]
    public AdminLogType type {get;set;}

    [FieldSelect]
    public string? text {get;set;}

    [FieldSelect]
    public DateTime createDate {get;set;}

    [FieldSelect]
    public long initiator {get;set;}

    [FieldSelect]
    public long target {get;set;}
}