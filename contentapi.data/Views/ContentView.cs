namespace contentapi.data.Views;

[ResultFor(RequestType.content)]
[SelectFrom("content AS main")]
[WriteAs(typeof(Db.Content))]
public class ContentView : IIdView
{
    public const string MessagesTable = "messages";
    public const string ContentTable = "content";
    public const string EngagementTable = "content_engagement";
    public const string NaturalCommentQuery = "deleted = 0 and module IS NULL";

    [DbField]
    public long id { get; set; }

    //Entirely not writable
    [DbField]
    public bool deleted { get; set; }

    [DbField]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId { get; set; }

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [DbField]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public InternalContentType contentType {get;set;}

    [DbField]
    [Writable]
    public string name { get; set; } = "";

    [DbField]
    [Writable]
    public long parentId { get; set; }

    [NoQuery]
    [DbField]
    [Writable]
    public string text { get; set; } = "";


    [DbField]
    [Writable] //Not for files though!
    public string? literalType {get;set;}   //The page type set by users, OR the file mimetype

    [DbField] 
    [Writable(WriteRule.User, WriteRule.Preserve)] // Don't know if this is what I want... 
    public string? meta {get;set;}          //Not always used, READONLY after insert

    [DbField] 
    [Writable]
    public string? description {get;set;}   //Tagline for pages, description for anything else maybe

    [DbField]  //This is special, because it MUST be unique! The API will manage it... hopefully
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public string hash {get;set;} = "";     //Some kind of unique public identifier. Uniqueness is enforced by the API however


    //NOtice the lack of "FieldSelect": this does NOT come from the standard query
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();

    //NOTE: values will have some content-specific things!
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<string, object> values {get;set;} = new Dictionary<string, object>();

    //Although these are NOT searchable with a standard search system, they do at least have 
    //macros to let you search. Essentially, any field that is a "list" or something else
    //will not be searchable.
    [NoQuery]
    [Writable]
    [Expensive(2)]
    public List<string> keywords {get;set;} = new List<string>();

    [NoQuery]
    [Expensive(2)]
    public Dictionary<string, Dictionary<string, int>> engagement {get;set;} = new Dictionary<string, Dictionary<string, int>>();

    //[NoQuery]
    //[Expensive(2)]
    //public Dictionary<string, string> myEngagement {get;set;} = new Dictionary<string, string>();

    [Expensive(1)]
    [DbField("select max(id) from " + MessagesTable + " where main.id = contentId and " + NaturalCommentQuery )]
    public long? lastCommentId {get;set;}

    [Expensive(3)]
    [DbField("select count(*) from " + MessagesTable + " where main.id = contentId and " + NaturalCommentQuery)]
    public int commentCount {get;set;}

    //Initially, this will be a hardcoded field. Sorry...
    //Ignore natural comment query to make it faster(?)
    [Expensive(3)]
    [DbField("select (select count(*) from " + MessagesTable + " where contentId=main.id) + 5 * (select count(*) from " +
        EngagementTable + " where contentId=main.id and engagement in (\"good\",\"+\",\"👍\")) - 3 * (select count(*) from " +
        EngagementTable + " where contentId=main.id and engagement in (\"bad\",\"-\",\"👎\"))")]
    public double popScore1 {get;set;}

    //[Expensive(3)]
    //[DbField("select count(*) from " + ContentTable + " where parentId = main.id and deleted = 0")]
    //public int childCount {get;set;}

    [Expensive(1)]
    [DbField("select count(*) from content_watches where main.id = contentId")]
    public int watchCount {get;set;}

    [Expensive(1)]
    [DbField("select count(*) from content_keywords where main.id = contentId")]
    public int keywordCount {get;set;}

    [Expensive(1)]
    [DbField("select max(id) from content_history where main.id = contentId")]
    public long lastRevisionId {get;set;} //ALL content has a lastRevisionId
}