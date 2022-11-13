using contentapi.Db;

namespace contentapi.data.Views;

[ResultFor(RequestType.content_engagement)]
[SelectFrom("content_engagement")]
[WriteAs(typeof(Db.ContentEngagement))]
public class ContentEngagementView : IContentUserRelatedView, IEngagementView
{
    [DbField]
    public long id { get; set; }

    [DbField]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    [DbField]
    [Writable]
    public string type { get; set; } = "";

    [DbField]
    [Writable]
    public string engagement { get; set; } = "";

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [DbField]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long contentId { get; set; }

    //An alias
    //[FieldSelect("contentId")]
    [FullIgnore]
    public long relatedId => contentId;

    public void SetRelatedId(long id) { contentId = id; }
}

[ResultFor(RequestType.message_engagement)]
[SelectFrom("message_engagement")] // e join messages m on e.messageId = m.id")]
[WriteAs(typeof(Db.MessageEngagement))]
public class MessageEngagementView : IContentUserRelatedView, IEngagementView
{
    [DbField()] //"e.id", "id")]
    public long id { get; set; }

    [DbField()] //"e.userId", "userId")]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId { get; set; }

    [DbField()] //"e.type", "type")]
    [Writable]
    public string type { get; set; } = "";

    [DbField()] //"e.engagement", "engagement")]
    [Writable]
    public string engagement { get; set; } = "";

    [DbField()] //"e.createDate", "createDate")]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [DbField()] //"e.messageId", "messageId")]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long messageId { get; set; }
    
    [Expensive(1)]
    [DbField("select contentId from messages m where m.id = messageId")] //"m.contentId", "contentId")]
    public long contentId {get;set;}

    //An alias
    //[FieldSelect("messageId")]
    [FullIgnore]
    public long relatedId => messageId;

    public void SetRelatedId(long id) { messageId = id; }
}