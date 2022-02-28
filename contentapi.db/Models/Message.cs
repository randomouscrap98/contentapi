using System;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("messages")]
    public class Message
    {
        [Key]
        public virtual long id { get; set; }
        public long contentId { get; set; }
        public long createUserId { get; set; }
        public DateTime createDate { get; set; }
        public string text { get; set; } = "";
        public DateTime? editDate { get; set; }
        public long? editUserId { get; set; }

        //module messages are just comments. You can search for comments by finding null modules
        public string module { get; set; }  //module is nullable
        public long receiveUserId { get; set; } //this doesn't matter for non-comments? Or MAYBE?

        //Store something in here which can be parsed to see
        //who has done what on this comment, just for admin purposes.
        //Don't need to restore or any of that.
        public string history { get; set; } //history is nullable
        public bool deleted { get; set; }
    }

    [Table("messages")]
    public class Message_Convert : Message
    {
        [ExplicitKey]
        public override long id {get;set;}
    }
}