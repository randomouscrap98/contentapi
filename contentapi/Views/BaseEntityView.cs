using System;

namespace contentapi.Views
{
    public class BaseEntityView : BaseView
    {
        public DateTime editDate {get;set;}
        public long createUserId {get;set;}
        public long editUserId {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var c = (BaseEntityView)obj;
            return base.EqualsSelf(obj) && c.editDate == editDate && c.createUserId == createUserId && c.editUserId == editUserId;
        }
    }
}