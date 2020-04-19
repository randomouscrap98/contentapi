using System;

namespace contentapi.Views
{
    public class BaseEntityView : BaseView
    {
        public DateTime editDate {get;set;}
        public long createUserId {get;set;}
        public long editUserId {get;set;}
    }
}