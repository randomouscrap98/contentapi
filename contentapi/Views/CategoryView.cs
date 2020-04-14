using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class CategoryView : PermissionView
    {
        [Required]
        public string name {get;set;}

        public string description {get;set;}

    }
}