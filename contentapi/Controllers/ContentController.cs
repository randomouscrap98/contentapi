using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace contentapi.Controllers
{
    public class ContentController : EntityController<ContentEntity, ContentView>
    {
        public ContentController(EntityControllerServices services) : base(services) { }

        protected override async Task<ContentEntity> Post_ConvertItemAsync(ContentView content)
        {
            var result = await base.Post_ConvertItemAsync(content);

            var category = await GetSingleBase(content.categoryId);

            if(category == null)
                ThrowAction(BadRequest("Must provide category for content!"));

            return result;
        }
    }
}
