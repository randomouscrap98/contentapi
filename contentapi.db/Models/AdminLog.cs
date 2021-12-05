using System;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("admin_log")]
    public class AdminLog
    {
        [Key]
        public long id { get; set; }
        public AdminLogType type { get; set; }
        public string text { get; set; }
        public DateTime createDate { get; set; }
        public long initiator { get; set; }
        public long target { get; set; }
    }
}