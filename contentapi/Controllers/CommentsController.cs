using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace contentapi.Controllers
{
    public class CommentsController : EntityController<CommentEntity, CommentView>
    {
        public CommentsController(EntityControllerServices services) : base(services) { }

        protected override async Task<CommentEntity> Post_ConvertItemAsync(CommentView comment)
        {
            var result = await base.Post_ConvertItemAsync(comment);
            await CheckRequiredEntityParentReadAsync(comment.parentId);
            return result;
        }
    }
}
