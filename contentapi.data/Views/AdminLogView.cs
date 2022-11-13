
namespace contentapi.data.Views;

[ResultFor(RequestType.adminlog)]
[SelectFrom("admin_log")]
public class AdminLogView : IIdView
{
    [DbField]
    public long id {get;set;}

    [DbField]
    public AdminLogType type {get;set;}

    [DbField]
    public string? text {get;set;}

    [DbField]
    public DateTime createDate {get;set;}

    [DbField]
    public long initiator {get;set;}

    [DbField]
    public long target {get;set;}
}