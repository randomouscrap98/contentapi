using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.Content))]
//[ForRequest(RequestType.module)]
[ResultFor(RequestType.module)]
//[SelectFrom("content")]
[Where("internalType = 2")]
//[WriteAs(typeof(Db.Content))]
public class ModuleView : ContentView
{
    //[FromField("content")]
    [NoQuery]
    [Multiline]
    [FieldSelect("content")]
    [Writable]
    public string code { get; set; } = "";

    //[FromField("extra1")] 
    [Multiline]
    [FieldSelect("extra1")]
    [Writable]
    public string? description {get;set;}
}