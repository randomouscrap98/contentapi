using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace contentapi.Controllers
{
    public class SubcontentController : EntityController<SubcontentEntity, SubcontentView>
    {
        public SubcontentController(EntityControllerServices services) : base(services) { }

        protected override async Task<SubcontentEntity> Post_ConvertItemAsync(SubcontentView subcontent)
        {
            var result = await base.Post_ConvertItemAsync(subcontent);

            var content = await GetSingleBase(subcontent.contentId);

            if(content == null)
                ThrowAction(BadRequest("Must provide content for post!"));

            return result;
        }
    }
}
