
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.page)]
[Where("internalType = 1")]
public class PageView : ContentView
{
    [FieldSelect("publicType")]
    [Writable]
    public string type { get; set; } = "";

    [NoQuery]
    [Multiline]
    [FieldSelect]
    [Writable]
    public string content { get; set; } = "";
}