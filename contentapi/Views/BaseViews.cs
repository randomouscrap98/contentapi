using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Extensions;

namespace contentapi.Views
{
    public class BaseView : IIdView
    {
        public long id {get;set;}

        protected virtual bool EqualsSelf(object obj)
        {
            var type = obj.GetType();

            //only public properties, that's fine, these are all views. ALSO no lists/dictionaries, those are on YOU to compare!
            var properties = type.GetProperties();
            
            foreach(var property in type.GetProperties())
            {
                if(!(property.PropertyType.IsGenericType && property.PropertyType.IsAssignableFrom(typeof(List<>)) || 
                    property.PropertyType.IsAssignableFrom(typeof(Dictionary<,>))))
                {
                    if(!property.GetValue(obj).Equals(property.GetValue(this)))
                        return false;
                }
            }

            return true;
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

    public class BasePermissionView : BaseView
    {
        /// <summary>
        /// All the permissions set on the view. The keys unfortunately have to be strings.
        /// </summary>
        /// <typeparam name="long"></typeparam>
        /// <typeparam name="string"></typeparam>
        /// <returns></returns>
        public Dictionary<string, string> permissions {get;set;} = new Dictionary<string, string>();

        public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

        protected override bool EqualsSelf(object obj)
        {
            var c = (BasePermissionView)obj;
            return base.EqualsSelf(obj) && c.permissions.RealEqual(permissions) && c.values.RealEqual(values);
        }
    }
}