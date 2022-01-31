using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Content))]
[ForRequest(RequestType.module)]
public class ModuleView : ContentView
{
    [FromField("content")]
    [Multiline]
    public string code { get; set; } = "";

    [FromField("extra1")] 
    [Multiline]
    public string? description {get;set;}
}