using System;

namespace contentapi.data.Views;

[ResultFor(RequestType.activity)]
[SelectFrom("content_history")]
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
}