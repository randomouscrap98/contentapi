using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.content)]
[SelectFrom("content AS main")]
[WriteAs(typeof(Db.Content))]
public class ContentView : IIdView
{
    public const string MessagesTable = "messages";
    public const string NaturalCommentQuery = "deleted = 0 and module IS NULL";

    [FieldSelect]
    public long id { get; set; }

    //Entirely not writable
    [FieldSelect]
    public bool deleted { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public InternalContentType contentType {get;set;}

    [FieldSelect]
    [Writable]
    public string name { get; set; } = "";

    [FieldSelect]
    [Writable]
    public long parentId { get; set; }

    [FieldSelect]
    [Writable]
    public string text { get; set; } = "";


    [FieldSelect]
    [Writable] //Not for files though!
    public string? literalType {get;set;}   //The page type set by users, OR the file mimetype

    [FieldSelect] //NEVER WRITABLE
    public string? meta {get;set;}          //Not always used, READONLY after insert

    [FieldSelect] 
    [Writable]
    public string? description {get;set;}   //Tagline for pages, description for anything else maybe

    [FieldSelect]  //This is special, because it MUST be unique! The API will manage it... hopefully
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
    public Dictionary<VoteType, int> votes {get;set;} = new Dictionary<VoteType, int>();

    [Expensive(1)]
    [FieldSelect("select createDate from " + MessagesTable + " where main.id = contentId and " + NaturalCommentQuery + " order by id desc limit 1")]
    public DateTime lastCommentDate {get;set;}

    [Expensive(1)]
    [FieldSelect("select id from " + MessagesTable + " where main.id = contentId and " + NaturalCommentQuery + " order by id desc limit 1")]
    public long lastCommentId {get;set;}

    [Expensive(1)]
    [FieldSelect("select count(*) from " + MessagesTable + " where main.id = contentId and " + NaturalCommentQuery)]
    public int commentCount {get;set;}

    [Expensive(1)]
    [FieldSelect("select count(*) from content_watches where main.id = contentId")]
    public int watchCount {get;set;}

    [Expensive(1)]
    [FieldSelect("select createDate from content_history where main.id = contentId order by id desc limit 1")]
    public DateTime lastRevisionDate {get;set;}

    [Expensive(1)]
    [FieldSelect("select id from content_history where main.id = contentId order by id desc limit 1")]
    public long lastRevisionId {get;set;}
}