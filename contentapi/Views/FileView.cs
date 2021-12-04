using contentapi.Search;

namespace contentapi.Views;

[ForRequest(RequestType.file)]
public class FileView : ContentView
{
    [Searchable]
    [FromField("publicType")]
    public string hash { get; set; } = "";

    [FromField("content")]
    public string mimetype { get; set; } = "";

    [FromField("extra1")]  //Quantization is string in case more info should be provided
    public string quantization {get;set;} = "";
}