using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class ContentView : PermissionView
    {
        [Required]
        [StringLength(128, MinimumLength=1)]
        public string title {get;set;}

        [Required]
        [StringLength(65536, MinimumLength = 2)]
        public string content {get;set;}

        public string type {get;set;}

        public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

        public List<string> keywords {get;set;} = new List<string>();
    }
}