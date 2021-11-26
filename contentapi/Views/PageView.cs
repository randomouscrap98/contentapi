
using contentapi.Search;

namespace contentapi.Views;

public class PageView : ContentView
{
    [Searchable]
    [FromField("publicType")]
    public string type { get; set; } = "";

    public string content { get; set; } = "";
}