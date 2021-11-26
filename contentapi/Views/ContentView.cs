using contentapi.Search;

namespace contentapi.Views;

[FromDb(typeof(Db.Content))]
public class ContentView
{
    [Searchable]
    public long id { get; set; }

    [Searchable]
    public bool deleted { get; set; }

    [Searchable]
    public long createUserId { get; set; }

    [Searchable]
    public DateTime createDate { get; set; }

    [Searchable]
    public string name { get; set; } = "";

    [Searchable]
    public long parentId { get; set; }

    //NOTE: values will have some content-specific things!
    [FromField("")] //Empty field means something special, these are removed from standard searches
    public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

    [FromField("")]
    public List<string> keywords {get;set;} = new List<string>();

    [Searchable]
    [FromField("")]
    public DateTime lastPostDate {get;set;}
}