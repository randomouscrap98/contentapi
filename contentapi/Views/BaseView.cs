using System;

namespace contentapi.Views
{
    public class IdView
    {
        public long id {get;set;}

        protected virtual bool EqualsSelf(object obj)
        {
            var other = (IdView)obj;
            return other.id == id;
        }

        public override bool Equals(object obj)
        {
            if(obj != null && this.GetType().Equals(obj.GetType()))
                return EqualsSelf(obj);
            else
                return false;
        }

        public override int GetHashCode() 
        { 
            return id.GetHashCode(); 
        }
    }

    public class BaseView : IdView
    {
        public DateTime createDate {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var other = (BaseView)obj;
            return base.EqualsSelf(obj) && other.createDate == createDate;
        }
    }
}