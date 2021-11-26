using contentapi.Search;

namespace contentapi.Views;

public class FileView : ContentView
{
    [Searchable]
    [FromField("publicType")]
    public string bucket { get; set; } = "";

    [Searchable]
    [FromField("content")]
    public string mimetype { get; set; } = "";

    [FromField("")]
    public int quantization {get;set;}
}