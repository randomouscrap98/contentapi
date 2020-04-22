using System;

namespace contentapi.Views
{
    public class ActivityView
    {
        public long id;
        public DateTime date;

        public long userId {get;set;}
        public long contentId {get;set;}

        public string contentType {get;set;}
        public string action {get;set;}
        public string extra {get;set;}
    }
}