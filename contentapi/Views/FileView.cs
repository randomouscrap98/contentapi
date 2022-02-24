using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.file)]
[Where("internalType = 3")] //WARN: 3 is "file" and hopefully will always stay like that BUT...
public class FileView : ContentView
{
    //Consider whether hashes should be searchable or not. Remember, if a file is private, it won't show up in the results anyway.
    [FieldSelect("publicType")]
    public string hash { get; set; } = "";

    [FieldSelect("text")]
    public string mimetype { get; set; } = "";

    [FieldSelect("extra1")]  //Quantization is string in case more info should be provided
    public string quantization {get;set;} = "";
}
