using System;

namespace contentapi.data.Views;

[ResultFor(RequestType.activity)]
[SelectFrom("content_history")]
public class ActivityView : IContentUserRelatedView
{
    [FieldSelect]
    public long id { get; set; }

    [FieldSelect]
    public long contentId { get; set; }

    [FieldSelect("createUserId")]
    public long userId { get; set; }

    [FieldSelect("createDate")]
    public DateTime date { get; set; }

    [FieldSelect]
    public string? message {get;set;}

    [FieldSelect]
    public UserAction action {get;set;}
}