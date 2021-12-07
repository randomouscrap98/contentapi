using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.ContentHistory))]
[ForRequest(RequestType.activity)]
public class ActivityView : IIdView
{
    [Searchable]
    public long id { get; set; }

    [Searchable]
    public long contentId { get; set; }

    [Searchable]
    [FromField("createUserId")]
    public long userId { get; set; }

    [Searchable]
    [FromField("createDate")]
    public DateTime date { get; set; }

    [Searchable]
    public string? message {get;set;}

    [Searchable]
    public UserAction action {get;set;}
}