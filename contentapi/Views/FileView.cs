using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Content))]
[ForRequest(RequestType.file)]
public class FileView : ContentView
{
    //Consider whether hashes should be searchable or not. Remember, if a file is private, it won't show up in the results anyway.
    [Searchable]
    [FromField("publicType")]
    [WriteRule(WriteRuleType.ReadOnly)] //Readonly is the catch-all for system generated values that the user can't actually set.
    public string hash { get; set; } = "";

    [FromField("content")]
    [WriteRule(WriteRuleType.ReadOnly)]
    public string mimetype { get; set; } = "";

    [FromField("extra1")]  //Quantization is string in case more info should be provided
    [WriteRule(WriteRuleType.ReadOnly)]
    public string quantization {get;set;} = "";
}
