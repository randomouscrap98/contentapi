using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Content))]
[ForRequest(RequestType.file)]
public class FileView : ContentView
{
    //Consider whether hashes should be searchable or not. Remember, if a file is private, it won't show up in the results anyway.
    [Searchable]
    [FromField("publicType")]
    public string hash { get; set; } = "";

    [FromField("content")]
    public string mimetype { get; set; } = "";

    [FromField("extra1")]  //Quantization is string in case more info should be provided
    public string quantization {get;set;} = "";
}
