using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class CategoryView : BasePermissionView
    {
        [Required]
        [StringLength(128, MinimumLength = 1)]
        public string name {get;set;}

        [StringLength(2048)]
        public string description {get;set;}
    }
}