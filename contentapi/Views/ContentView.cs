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

    //public InternalContentType internalType { get; set; }

    [Searchable]
    [FromField("publicType")]
    public string type { get; set; } = "";

    [Searchable]
    public string name { get; set; } = "";

    public string content { get; set; } = "";

    [Searchable]
    public long parentId { get; set; }

    //These are all fields you can request, but they are special additions.
    [FromField("")] //Empty field means something special, this is an additional query.
    public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

    [FromField("")]
    public List<string> keywords {get;set;} = new List<string>();
}