using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using contentapi.Services.Extensions;

namespace contentapi.Views
{
    public class ModuleView : BaseView, IEditView, IValueView
    {
        [Required]
        [StringLength(32, MinimumLength = 1)]
        public string name {get;set;}

        [Required]
        public string code {get;set;}

        public string description {get;set;}

        public DateTime editDate { get ; set; }
        public long createUserId { get ; set ; }
        public long editUserId { get ; set ; }
        public DateTime createDate { get ; set ; }

        public Dictionary<string, string> values { get ; set ; } = new Dictionary<string, string>();

        protected override bool EqualsSelf(object obj)
        {
            var o = (ModuleView)obj;
            return base.EqualsSelf(obj) && values.RealEqual(o.values);
        }
    }
}