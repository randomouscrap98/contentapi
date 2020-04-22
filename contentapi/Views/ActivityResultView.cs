using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    public class ActivityResultView
    {
        public List<ActivityView> activity {get;set;}
        public List<CommentActivity> comments {get;set;}
        //public List<UserViewBasic>  userData {get;set;}
    }

    public class CommentActivity
    {
        public long parentId {get;set;}
        public int count {get;set;}
        public DateTime lastDate {get;set;}
        public List<long> userIds {get;set;}
    }

}