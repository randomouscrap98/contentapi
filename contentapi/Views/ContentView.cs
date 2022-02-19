using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.Content))]
//[ForRequest(RequestType.content)]

[ResultFor(RequestType.content)]
[SelectFrom("content")]
public class ContentView : IIdView
{
    //[Searchable]
    //[WriteRule(WriteRuleType.None)] //The first parameter is for inserts, and the default write rule for updates (2nd param) is preserve, so...
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.ReadOnly)]
    //Entirely not writable
    [FieldSelect]
    public bool deleted { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.AutoUserId)] 
    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.AutoDate)] 
    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    //[Searchable]
    [FieldSelect]
    //Entirely not writable
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]  //This is set by the system, the user has no control over it.
    public InternalContentType internalType {get;set;}

    //[Searchable]
    [FieldSelect]
    [Writable]
    public string name { get; set; } = "";

    //[Searchable]
    [FieldSelect]
    [Writable]
    public long parentId { get; set; }


    //[Computed] //REMEMBER: computed is reserved for things that can't be part of the standard query builder, that's all! Many fields CAN be, see below
    //NOtice the lack of "FieldSelect": this does NOT come from the standard query
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();

    //NOTE: values will have some content-specific things!
    //[Computed]
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

    //Although these are NOT searchable with a standard search system, they do at least have 
    //macros to let you search. Essentially, any field that is a "list" or something else
    //will not be searchable.
    //[Computed]
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public List<string> keywords {get;set;} = new List<string>();

    //[Computed]
    [NoQuery]
    [Expensive(2)]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public Dictionary<VoteType, int> votes {get;set;} = new Dictionary<VoteType, int>();


    //[Searchable]
    [Expensive(1)]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public DateTime lastCommentDate {get;set;}

    //[Searchable]
    [Expensive(1)]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public long lastCommentId {get;set;}

    //[Searchable]
    [Expensive(1)]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public int commentCount {get;set;}

    //[Searchable]
    [Expensive(1)]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public int watchCount {get;set;}

    //[Searchable]
    [Expensive(1)]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public DateTime lastRevisionDate {get;set;}

    //[Searchable]
    [Expensive(1)]
    //[WriteRule(WriteRuleType.ReadOnly, WriteRuleType.ReadOnly)]
    public long lastRevisionId {get;set;}
}