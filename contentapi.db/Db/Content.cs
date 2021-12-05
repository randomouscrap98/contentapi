using System;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("content")]
    public class Content
    {
        [Key]
        public virtual long id { get; set; }
        public bool deleted { get; set; }
        public long createUserId { get; set; }
        public DateTime createDate { get; set; }
        public InternalContentType internalType { get; set; }
        public string publicType { get; set; } = "";
        public string name { get; set; } = "";
        public string content { get; set; } = "";
        public long parentId { get; set; }

        public string extra1 { get; set; }
    }

    [Table("content")]
    public class Content_Convert : Content
    {
        [ExplicitKey]
        public override long id {get;set;}
    }
}
