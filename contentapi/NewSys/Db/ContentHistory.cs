using System;

namespace contentapi.Db
{
    public class ContentHistory
    {
        public long id {get;set;}
        public long contentId {get;set;}
        public UserAction action {get;set;}

        //Some kind of storage format. Kind of wasteful for initial
        //posts, since it will be duplicated, but it's just easier to
        //keep a copy of every "revision" made than try to optimize
        //for the single active one
        public string snapshot {get;set;}

        // The user that did the actions and when
        public long createUserId {get;set;}
        public DateTime createDate {get;set;}
    }
}