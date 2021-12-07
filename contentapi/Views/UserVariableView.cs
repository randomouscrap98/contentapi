using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.UserVariable))]
[ForRequest(RequestType.uservariable)]
public class UserVariableView : IIdView
{
    [Searchable]
    public long id {get;set;}

    [Searchable]
    public long userId {get;set;}

    [Searchable]
    public DateTime createDate { get; set; }

    [Searchable]
    public DateTime? editDate { get; set; }

    [Searchable]
    public long editCount { get; set; }

    [Searchable]
    public string key {get;set;} = "";

    public string value {get;set;} = "";
}