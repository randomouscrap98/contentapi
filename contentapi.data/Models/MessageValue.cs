using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("message_values")]
    public class MessageValue
    {
        [Key]
        public long id { get; set; }
        public long messageId { get; set; }
        public string key { get; set; } = "";
        public string value { get; set; } = "";
    }
}
