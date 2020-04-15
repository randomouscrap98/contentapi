using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    public class PermissionView: ViewBase
    {
        /// <summary>
        /// The creator of the view
        /// </summary>
        /// <value></value>
        public long userId {get;set;}

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
    }
}