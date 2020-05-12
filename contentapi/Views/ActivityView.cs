using System;

namespace contentapi.Views
{
    public class ActivityView : IdView
    {
        public DateTime date {get;set;}

        public long userId {get;set;}
        public long contentId {get;set;}

        public string contentType {get;set;}
        public string action {get;set;}
        public string extra {get;set;}
    }
}