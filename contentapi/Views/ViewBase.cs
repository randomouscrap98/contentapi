using System;

namespace contentapi.Views
{
    public class ViewBase
    {
        public long id {get;set;}
        public DateTimeOffset editDate {get;set;}
        public DateTimeOffset createDate {get;set;}
    }
}