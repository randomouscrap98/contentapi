using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
    }
}