using contentapi.Search;

namespace contentapi.Views;

public class ModuleView : ContentView
{
    [FromField("content")]
    public string code { get; set; } = "";

    [FromField("")] //From values
    public int description {get;set;}
}