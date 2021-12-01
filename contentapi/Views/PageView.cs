
using contentapi.Search;

namespace contentapi.Views;

[FromRequest(RequestType.page)]
public class PageView : ContentView
{
    [Searchable]
    [FromField("publicType")]
    public string type { get; set; } = "";

    public string content { get; set; } = "";
}