using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    public class ActivityView : BaseView
    {
        public DateTime date {get;set;}

        public long userId {get;set;}
        public long contentId {get;set;}

        public string type {get;set;}
        public string contentType {get;set;}
        public string action {get;set;}
        public string extra {get;set;}
    }

    public class ActivityAggregateView : IIdView
    {
        public long id {get;set;} //This is PARENT id
        public int count {get;set;}
        public DateTime? firstActivity {get;set;}
        public DateTime? lastActivity {get;set;}
        public List<long> userIds {get;set;}
        //public Dictionary<string,string> userActions {get;set;}
    }
}