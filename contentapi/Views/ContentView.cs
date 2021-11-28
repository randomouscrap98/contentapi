using contentapi.Db;
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
    public string internalType {get;set;} = "";

    [Searchable]
    public string name { get; set; } = "";

    [Searchable]
    public long parentId { get; set; }

    [FromField("")]
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();

    //NOTE: values will have some content-specific things!
    [FromField("")] //Empty field means something special, their field can be included but is not mapped to any particular field in the database
    public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

    //Although these are NOT searchable with a standard search system, they do at least have 
    //macros to let you search. Essentially, any field that is a "list" or something else
    //will not be searchable.
    [FromField("")]
    public List<string> keywords {get;set;} = new List<string>();

    [FromField("")]
    public Dictionary<string, int> votes {get;set;} = new Dictionary<string, int>();

    [Searchable]
    [FromField("")]
    public DateTime lastCommentDate {get;set;}

    [Searchable]
    [FromField("")]
    public long lastCommentId {get;set;}

    [Searchable]
    [FromField("")]
    public int commentCount {get;set;}

    [Searchable]
    [FromField("")]
    public int watchCount {get;set;}
}