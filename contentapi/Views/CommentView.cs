using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.Comment))]
[ForRequest(RequestType.comment)]
public class CommentView
{
    [Searchable]
    public long id {get;set;}

    [Searchable]
    public long contentId {get;set;}

    [Searchable]
    public long createUserId {get;set;}

    [Searchable] //Maybe a problem
    public DateTime createDate {get;set;}

    [Searchable] //Maybe a super bad idea
    public string text {get;set;} = "";

    [Searchable]
    public DateTime? editDate {get;set;}

    [Searchable]
    public long? editUserId {get;set;}

    [Searchable]
    public bool deleted {get;set;}
}