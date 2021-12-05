using System;

namespace contentapi.Db.History
{
    public class CommentSnapshot
    {

        public long userId {get;set;}
        public UserAction action {get;set;}
        public DateTime editDate {get;set;}
        public string previous {get;set;}
    }
}