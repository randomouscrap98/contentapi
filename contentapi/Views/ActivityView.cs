using contentapi.Search;

namespace contentapi.Views;

[FromDb(typeof(Db.ContentHistory))]
public class ActivityView
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
    public string action {get;set;} = "";
}