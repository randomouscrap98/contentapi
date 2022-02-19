
using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.Content))]
//[ForRequest(RequestType.page)]
[ResultFor(RequestType.page)]
//[SelectFrom("content")]
[Where("internalType = 1")]
//[WriteAs(typeof(Db.Content))]
public class PageView : ContentView
{
    //[Searchable]
    //[FromField("publicType")]
    [FieldSelect("publicType")]
    [Writable]
    public string type { get; set; } = "";

    [NoQuery]
    [Multiline]
    [FieldSelect]
    [Writable]
    public string content { get; set; } = "";
}