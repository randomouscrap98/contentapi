
namespace contentapi.data.Views;

[ResultFor(RequestType.message)]
[SelectFrom("messages")]
[WriteAs(typeof(Db.Message))]
public class MessageView : IContentRelatedView
{
    [DbField]
    //[Writable(WriteRule.Preserve, WriteRule.Preserve)] //Why preserve? So it's forced to be 0
    public long id {get;set;}

    [DbField]
    //[Writable(WriteRule.User, WriteRule.Preserve)] //This USED to be preserved, but now that we can rethread, we don't want to
    [Writable()]
    public long contentId {get;set;}

    [DbField]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId {get;set;}

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate {get;set;}

    [Multiline]
    [DbField]
    [Writable]
    public string text {get;set;} = "";

    //These three fields were originally part of the metadata

    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<string, object> values {get;set;} = new Dictionary<string, object>();

    [NoQuery]
    [Expensive(2)]
    public Dictionary<string, Dictionary<string, int>> engagement {get;set;} = new Dictionary<string, Dictionary<string, int>>();

    //[NoQuery]
    //[Expensive(2)]
    //public Dictionary<string, string> myEngagement {get;set;} = new Dictionary<string, string>();


    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate {get;set;}

    [DbField]
    [Writable(WriteRule.Preserve, WriteRule.AutoUserId)]
    public long? editUserId {get;set;}

    [DbField("history IS NOT NULL AND editUserId != 0")]
    public bool edited {get;set;}

    [DbField]
    //Note: completely not writable
    public bool deleted {get;set;}

    [DbField] 
    [Writable(WriteRule.User, WriteRule.Preserve)] //Is this necessary?
    public string? module { get; set; }

    [DbField]
    [Writable(WriteRule.User, WriteRule.Preserve)] //Is this necessary?
    public long receiveUserId { get; set; } 

    [NoQuery]
    [Expensive(1)]
    public List<long> uidsInText {get;set;} = new List<long>();
}