using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Content))]
[ForRequest(RequestType.content)]
public class ContentView : IIdView
{
    [Searchable]
    //[WriteRule(WriteRuleType.None)] //The first parameter is for inserts, and the default write rule for updates (2nd param) is preserve, so...
    public long id { get; set; }

    [Searchable]
    public bool deleted { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.AutoUserId)] 
    public long createUserId { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.AutoDate)] 
    public DateTime createDate { get; set; }

    [Searchable]
    public InternalContentType internalType {get;set;}

    [Searchable]
    public string name { get; set; } = "";

    [Searchable]
    public long parentId { get; set; }


    //[FromField("")] //NOTE: This is not strictly required, but it's for safety: we want to avoid the default that says every field maps to a database column
    [Computed] //REMEMBER: computed is reserved for things that can't be part of the standard query builder, that's all! Many fields CAN be, see below
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();

    //NOTE: values will have some content-specific things!
    //[FromField("")]
    [Computed]
    public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

    //Although these are NOT searchable with a standard search system, they do at least have 
    //macros to let you search. Essentially, any field that is a "list" or something else
    //will not be searchable.
    //[FromField("")]
    [Computed]
    public List<string> keywords {get;set;} = new List<string>();

    //[FromField("")]
    [Computed]
    [WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public Dictionary<string, int> votes {get;set;} = new Dictionary<string, int>();


    [Searchable]
    //[FromField("")]
    [WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public DateTime lastCommentDate {get;set;}

    [Searchable]
    //[FromField("")]
    [WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public long lastCommentId {get;set;}

    [Searchable]
    //[FromField("")] 
    [WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public int commentCount {get;set;}

    [Searchable]
    //[FromField("")]
    [WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public int watchCount {get;set;}

    [Searchable]
    //[FromField("")]
    [WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public DateTime lastRevisionDate {get;set;}

    [Searchable]
    //[FromField("")]
    [WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public long lastRevisionId {get;set;}
}