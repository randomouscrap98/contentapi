using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class FileView : BasePermissionView
    {
        //[Required]
        //[StringLength(128, MinimumLength = 1)]
        //public string originalName {get;set;}

        [MaxLength(128)]
        public string name {get;set;}

        //This is ignored if changed by the user.
        public string fileType {get;set;}
    }
}