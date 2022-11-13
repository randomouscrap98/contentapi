using contentapi.Db;

namespace contentapi.data.Views;

[ResultFor(RequestType.content_engagement)]
[SelectFrom("content_engagement")]
[WriteAs(typeof(Db.ContentEngagement))]
public class ContentEngagementView : IContentUserRelatedView, IEngagementView
{
    [FieldSelect]
    public long id { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    [FieldSelect]
    [Writable]
    public string type { get; set; } = "";

    [FieldSelect]
    [Writable]
    public string engagement { get; set; } = "";

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId { get; set; }

    //An alias
    [FieldSelect("contentId")]
    public long relatedId => contentId;

    public void SetRelatedId(long id) { contentId = id; }
}

[ResultFor(RequestType.message_engagement)]
[SelectFrom("message_engagement e join message m on e.messsageId = m.id")]
[WriteAs(typeof(Db.MessageEngagement))]
public class MessageEngagementView : IContentUserRelatedView, IEngagementView
{
    [FieldSelect("e.id")]
    public long id { get; set; }

    [FieldSelect("e.userId")]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    [FieldSelect("e.type")]
    [Writable]
    public string type { get; set; } = "";

    [FieldSelect("e.engagement")]
    [Writable]
    public string engagement { get; set; } = "";

    [FieldSelect("e.createDate")]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [FieldSelect("e.messageId")]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long messageId { get; set; }
    
    [FieldSelect("m.contentId")]
    public long contentId {get;set;}

    //An alias
    [FieldSelect("messageId")]
    public long relatedId => messageId;

    public void SetRelatedId(long id) { messageId = id; }
}