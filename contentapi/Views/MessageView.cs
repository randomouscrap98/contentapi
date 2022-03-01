using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.message)]
[SelectFrom("messages")]
[WriteAs(typeof(Db.Message))]
public class MessageView : IIdView
{
    [FieldSelect]
    //[Writable(WriteRule.Preserve, WriteRule.Preserve)] //Why preserve? So it's forced to be 0
    public long id {get;set;}

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId {get;set;}

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate {get;set;}

    [Multiline]
    [FieldSelect]
    [Writable]
    public string text {get;set;} = "";

    //These three fields were originally part of the metadata

    [NoQuery]
    [Writable]
    [Expensive(2)]
    public Dictionary<string, object> values {get;set;} = new Dictionary<string, object>();

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate {get;set;}

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoUserId)]
    public long? editUserId {get;set;}

    [FieldSelect]
    //Note: completely not writable
    public bool deleted {get;set;}

    [FieldSelect] //Definitely not writable under any circumstances
    [Writable(WriteRule.Preserve, WriteRule.Preserve)] //Is this necessary?
    public string? module { get; set; }

    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)] //Is this necessary?
    public long receiveUserId { get; set; } 
}