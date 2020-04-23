using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    public class ActivityResultView
    {
        public List<ActivityView> activity {get;set;} = new List<ActivityView>();
        public List<CommentActivityView> comments {get;set;} = new List<CommentActivityView>();
        //public List<UserViewBasic>  userData {get;set;}
    }

    public class CommentActivityView
    {
        public long parentId {get;set;}
        public int count {get;set;}
        public DateTime lastDate {get;set;}
        public List<long> userIds {get;set;}
    }

}