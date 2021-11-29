
using contentapi.Search;

namespace contentapi.Views;

[FromDb(typeof(Db.ContentWatch))]
public class WatchView
{
    [Searchable]
    public long id { get; set; }

    [Searchable]
    public long contentId { get; set; }

    [Searchable]
    public long userId { get; set; }

    [Searchable]
    public long lastCommentId { get; set; }

    [Searchable]
    public long lastActivityId { get; set; }

    [Searchable]
    public DateTime createDate { get; set; }

    [Searchable]
    public DateTime editDate { get; set; }
}