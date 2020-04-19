using System;

namespace contentapi.Views
{
    public class ViewBaseLow
    {
        public long id {get;set;}
        public DateTime createDate {get;set;}
    }

    public class ViewBase : ViewBaseLow
    {
        public DateTime editDate {get;set;}
    }
}