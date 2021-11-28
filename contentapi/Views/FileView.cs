using contentapi.Search;

namespace contentapi.Views;

public class FileView : ContentView
{
    [Searchable]
    [FromField("publicType")]
    public string hash { get; set; } = "";

    [FromField("content")]
    public string mimetype { get; set; } = "";

    [FromField("")]
    public int quantization {get;set;}
}