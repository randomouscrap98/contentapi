using System;

namespace contentapi.Db
{
    public class Comment
    {
        public long id {get;set;}
        public long contentId {get;set;}
        public long createUserId {get;set;}
        public DateTime createDate {get;set;}
        public long? receiveUserId {get;set;}
        public string text {get;set;}
        public DateTime? editDate {get;set;}
        public long? editUserId {get;set;}

        //Store something in here which can be parsed to see
        //who has done what on this comment, just for admin purposes.
        //Don't need to restore or any of that.
        public string history {get;set;}
        public bool deleted {get;set;}
    }
}