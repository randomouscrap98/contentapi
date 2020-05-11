using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace contentapi.Views
{
    public class ContentView : BasePermissionView
    {
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
            return base.EqualsSelf(obj) && o.name == name && o.content == content && o.type == type &&
                o.keywords.OrderBy(x => x).SequenceEqual(keywords.OrderBy(x => x));
        }
    }
}