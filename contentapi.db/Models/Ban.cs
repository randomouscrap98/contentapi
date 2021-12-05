using System;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("bans")]
    public class Ban
    {
        [Key]
        public long id { get; set; }
        public DateTime createDate { get; set; }
        public DateTime expireDate { get; set; }
        public long createUserId { get; set; }
        public long bannedUserId { get; set; }
        public string message { get; set; } //message is nullable!
        public BanType type { get; set; }
    }
}
