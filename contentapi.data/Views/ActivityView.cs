using System;

namespace contentapi.data.Views;

[ResultFor(RequestType.activity)]
[SelectFrom("content_history AS main")]
public class ActivityView : IContentUserRelatedView
{
    [DbField]
    public long id { get; set; }

    [DbField]
    public long contentId { get; set; }

    [DbField("createUserId")]
    public long userId { get; set; }

    [DbField("createDate")]
    public DateTime date { get; set; }

    [DbField]
    public string? message {get;set;}

    [DbField]
    public UserAction action {get;set;}

    //[Expensive(3)]
    //[DbField("select c.literalType from content as c where main.contentId = c.id")]
    //public string? content_literalType {get;set;}

    //[Expensive(3)]
    //[DbField("select c.contentType from content as c where main.contentId = c.id")]
    //public InternalContentType content_contentType {get;set;}
}