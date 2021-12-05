using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Content))]
[ForRequest(RequestType.module)]
public class ModuleView : ContentView
{
    [FromField("content")]
    public string code { get; set; } = "";

    [FromField("extra1")] 
    public int description {get;set;}
}