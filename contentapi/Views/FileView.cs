using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.Content))]
//[ForRequest(RequestType.file)]
[ResultFor(RequestType.file)]
[SelectFrom("content")]
[Where("internalType = 3")] //WARN: 3 is "file" and hopefully will always stay like that BUT...
public class FileView : ContentView
{
    //Consider whether hashes should be searchable or not. Remember, if a file is private, it won't show up in the results anyway.
    //[Searchable]
    //[FromField("publicType")]
    //[WriteRule(WriteRuleType.ReadOnly)] //Readonly is the catch-all for system generated values that the user can't actually set.
    [FieldSelect("publicType")]
    public string hash { get; set; } = "";

    //[FromField("content")]
    //[WriteRule(WriteRuleType.ReadOnly)]
    [FieldSelect("content")]
    public string mimetype { get; set; } = "";

    //[FromField("extra1")]  //Quantization is string in case more info should be provided
    //[WriteRule(WriteRuleType.ReadOnly)]
    [FieldSelect("extra1")]  //Quantization is string in case more info should be provided
    public string quantization {get;set;} = "";
}
