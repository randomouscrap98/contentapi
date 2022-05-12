using contentapi.data;
using contentapi.Db;

namespace contentapi.History;

public class CommentSnapshot
{
    public long userId {get;set;}
    public UserAction action {get;set;}
    public DateTime editDate {get;set;}
    public string? previous {get;set;}
    public long contentId {get;set;}

    public List<MessageValue> values {get;set;} = new List<MessageValue>();
}