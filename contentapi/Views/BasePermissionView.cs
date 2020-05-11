using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    public class BasePermissionView: BaseEntityView
    {
        /// <summary>
        /// The direct parent of the view (can be empty sometimes!)
        /// </summary>
        /// <value></value>
        public long parentId {get;set;}

        /// <summary>
        /// All the permissions set on the view. The keys unfortunately have to be strings.
        /// </summary>
        /// <typeparam name="long"></typeparam>
        /// <typeparam name="string"></typeparam>
        /// <returns></returns>
        public Dictionary<string, string> permissions {get;set;} = new Dictionary<string, string>();

        public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

        /// <summary>
        /// This is a readonly field technically. I know that makes the API confusing but... ugh don't have TIMMEEE
        /// </summary>
        /// <value></value>
        public string myPerms {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var c = (BasePermissionView)obj;
            return base.EqualsSelf(obj) && c.parentId == parentId && c.myPerms == myPerms && c.permissions.Equals(permissions) &&
                c.values.Equals(values);
        }
    }
}