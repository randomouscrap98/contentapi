using System;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("user_relations")]
    public class UserRelation
    {
        [Key]
        public long id { get; set; }
        public UserRelationType type { get; set; }
        public DateTime createDate { get; set; }
        public long userId { get; set; }
        public long relatedId { get; set; }
    }
}
