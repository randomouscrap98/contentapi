using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class ContentView : PermissionView
    {
        [Required]
        public string title {get;set;}

        [Required]
        public string content {get;set;}

        public string type {get;set;}

        public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();

        public List<string> keywords {get;set;} = new List<string>();
    }
}