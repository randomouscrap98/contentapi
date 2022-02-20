using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.module)]
[Where("internalType = 2")]
public class ModuleView : ContentView
{
    [NoQuery]
    [Multiline]
    [FieldSelect("content")]
    [Writable]
    public string code { get; set; } = "";

    [Multiline]
    [FieldSelect("extra1")]
    [Writable]
    public string? description {get;set;}
}