using System;
using contentapi.data;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("content_history")]
    public class ContentHistory
    {
        [Key]
        public long id { get; set; }
        public long contentId { get; set; }
        public UserAction action { get; set; }

        //Some kind of storage format. Kind of wasteful for initial
        //posts, since it will be duplicated, but it's just easier to
        //keep a copy of every "revision" made than try to optimize
        //for the single active one
        public byte[] snapshot { get; set; } = new byte[0];
        public int snapshotVersion { get; set; }

        //Very helpful, users can put edit messages or the system can generate the revision redo messages
        public string? message {get;set;} //This can be null!

        // The user that did the actions and when
        public long createUserId { get; set; }
        public DateTime createDate { get; set; }
    }
}
