using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.ContentHistory))]
//[ForRequest(RequestType.comment_aggregate)]
[SelectFrom("something join something else?")]
[ResultFor(RequestType.comment_aggregate)]
public class CommentAggregateView : IIdView
{
    public long contentId { get; set; }

    public long createUserId { get; set; }


    public long id { get; set; }

    //[FromField("createDate")]
    public DateTime date { get; set; }

    //[Queryable]
    //public string? message {get;set;}

    public UserAction action {get;set;}
}