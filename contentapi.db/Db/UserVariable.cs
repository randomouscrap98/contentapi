using System;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("user_variables")]
    public class UserVariable
    {
        [Key]
        public long id { get; set; }
        public long userId { get; set; }
        public DateTime createDate { get; set; }
        public DateTime? editDate { get; set; }
        public long editCount { get; set; }
        public string key { get; set; } = "";
        public string value { get; set; } //allow null user variables
    }
}
