using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace contentapi.Models
{
    public enum LogAction
    {
        View = 1,
        Create,
        Read,
        Update,
        Delete
    }

    [Table("log")]
    public class ActionLog
    {
        public long actionUserId {get;set;}
        public LogAction action {get;set;}
        public long? contentId {get;set;}
        public long? categoryId {get;set;}
        public long? userId {get;set;}
        public DateTime createDate {get;set;}

        [ForeignKey("actionuserId")]
        public virtual User ActionUser {get;set;}
    }
}