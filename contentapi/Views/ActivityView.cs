using contentapi.Search;

namespace contentapi.Views;

[FromDb(typeof(Db.ContentHistory))]
[FromRequest(RequestType.activity)]
public class ActivityView
{
    [Searchable]
    public long id { get; set; }

    [Searchable]
    public long contentId { get; set; }

    [Searchable]
    [FromField("createUserId")]
    public long userId { get; set; }

    [Searchable]
    [FromField("createDate")]
    public DateTime date { get; set; }

    //WARN: You may want to make the enum fields like this ACTUALLY enums, but then when users want to search in the 
    //database for... well wait, that will be slow.
    //It's fine; make macros for the important ones.
    [Searchable]
    public string action {get;set;} = "";
}