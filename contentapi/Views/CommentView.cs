using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class CommentView : BaseEntityView
    {
        [Required]
        public long parentId {get;set;}

        [Required]
        [StringLength(4096, MinimumLength=2)]
        public string content {get;set;}
    }
}