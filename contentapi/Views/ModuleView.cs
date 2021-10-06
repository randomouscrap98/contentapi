using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using contentapi.Services;
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

        //Note: subcommands just have to go in the lua script itself, it's too much work to get this into the database. However,
        //they are added in for module get (as in, you don't have to add those values in POST)
        public Dictionary<string, ModuleSubcommandInfo> subcommands {get;set;}
        public bool Loaded {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var o = (ModuleView)obj;
            return base.EqualsSelf(obj) && values.RealEqual(o.values); // && subcommands.RealEqual(o.subcommands);
        }
    }
}