using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using contentapi.Services.Extensions;

namespace contentapi.Views
{
    ///// <summary>
    ///// A description of a module subcommand, which helps the module system 
    ///// do automation. All subcommand definitions are optional.
    ///// </summary>
    //public class ModuleSubcommand : CompareBase
    //{
    //    public string function {get;set;}
    //    public string description {get;set;}
    //    public List<string> args {get;set;}

    //    protected override bool EqualsSelf(object obj)
    //    {
    //        var o = (ModuleSubcommand)obj;
    //        return base.EqualsSelf(obj) && args.SequenceEqual(o.args);
    //    }
    //}

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

        //Note: subcommands just have to go in the lua script itself, it's too much work to get this into the database
        //public Dictionary<string, ModuleSubcommand> subcommands {get;set;} = new Dictionary<string, ModuleSubcommand>();

        protected override bool EqualsSelf(object obj)
        {
            var o = (ModuleView)obj;
            return base.EqualsSelf(obj) && values.RealEqual(o.values); // && subcommands.RealEqual(o.subcommands);
        }
    }
}