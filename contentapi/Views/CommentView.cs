using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class CommentView : ViewBase
    {
        [Required]
        public long parentId {get;set;}

        public long userId {get;set;}

        [Required]
        [StringLength(4096, MinimumLength=2)]
        public string content {get;set;}
    }
}