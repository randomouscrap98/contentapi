using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Extensions;

namespace contentapi.Views
{
    public class BaseView : CompareBase, IIdView
    {
        public long id {get;set;}

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

    public class StandardView : BasePermissionView, IEditView, IPermissionView, IValueView
    {
        public long parentId { get; set; }

        public DateTime createDate { get; set;}
        public DateTime editDate { get;set;}
        public long createUserId { get;set;} 
        public long editUserId { get;set;}

        [IgnoreCompare]
        public string myPerms { get; set; }
    }
}