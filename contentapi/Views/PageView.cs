
using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Content))]
[ForRequest(RequestType.page)]
public class PageView : ContentView
{
    [Searchable]
    [FromField("publicType")]
    public string type { get; set; } = "";

    public string content { get; set; } = "";
}