using System;

namespace contentapi.Views
{
    public class ViewBaseLow
    {
        public long id {get;set;}
        public DateTimeOffset createDate {get;set;}
    }

    public class ViewBase : ViewBaseLow
    {
        public DateTimeOffset editDate {get;set;}
    }
}