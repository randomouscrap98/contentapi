using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("comment_values")]
    public class CommentValue
    {
        [Key]
        public long id { get; set; }
        public long contentId { get; set; }
        public string key { get; set; } = "";
        public string value { get; set; } = "";
    }
}
