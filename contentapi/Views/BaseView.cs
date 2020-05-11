using System;

namespace contentapi.Views
{
    public class BaseView
    {
        public long id {get;set;}
        public DateTime createDate {get;set;}

        protected virtual bool EqualsSelf(object obj)
        {
            var other = (BaseView)obj;
            return other.id == id && other.createDate == createDate;
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
}