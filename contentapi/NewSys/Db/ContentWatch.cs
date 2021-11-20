using System;

namespace contentapi.Db
{
    public class ContentWatch
    {
        public long id {get;set;}
        public long contentId {get;set;}
        public long userId {get;set;}
        public long lastCommentId {get;set;}
        public long lastActivityId {get;set;}
        public DateTime createDate {get;set;}
        public DateTime editDate {get;set;}
    }
}