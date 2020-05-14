using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace contentapi.Views
{
    public class ContentView : BasePermissionView, IEditView, IPermissionView, IValueVlue
    {
        public long parentId { get; set; }

        public DateTime createDate { get; set;}
        public DateTime editDate { get;set;}
        public long createUserId { get;set;} 
        public long editUserId { get;set;}

        public string myPerms { get;set;}

        [Required]
        [StringLength(128, MinimumLength=1)]
        public string name {get;set;}

        [Required]
        [StringLength(65536, MinimumLength = 2)]
        public string content {get;set;}

        public string type {get;set;}

        public List<string> keywords {get;set;} = new List<string>();

        protected override bool EqualsSelf(object obj)
        {
            var o = (ContentView)obj;
            return base.EqualsSelf(obj) && o.keywords.OrderBy(x => x).SequenceEqual(keywords.OrderBy(x => x));
        }
    }
}