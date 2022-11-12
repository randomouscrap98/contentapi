using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    public class BaseEngagement
    {
        [Key]
        public long id { get; set; }
        public long userId { get; set; }
        public string type { get; set; } = "";
        public string engagement { get; set; } = "";
        public DateTime createDate { get; set; }
    }

    [Table("content_engagement")]
    public class ContentEngagement : BaseEngagement
    {
        public long contentId { get; set; }
    }

    [Table("message_engagement")]
    public class MessageEngagement : BaseEngagement
    {
        public long messageId { get; set; }
    }
}
