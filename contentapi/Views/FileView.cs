using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Content))]
[ForRequest(RequestType.file)]
public class FileView : ContentView
{
    //Consider whether hashes should be searchable or not. Remember, if a file is private, it won't show up in the results anyway.
    [Searchable]
    [FromField("publicType")]
    [WriteRule(WriteRuleType.None)] //None is a required placeholder when SOMETHING ELSE generates the insert value. Can't be default, otherwise the service overwrites what you might've provided
    public string hash { get; set; } = "";

    [FromField("content")]
    [WriteRule(WriteRuleType.None)]
    public string mimetype { get; set; } = "";

    [FromField("extra1")]  //Quantization is string in case more info should be provided
    [WriteRule(WriteRuleType.None)]
    public string quantization {get;set;} = "";
}
