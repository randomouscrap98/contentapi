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
            await CheckRequiredParentReadAsync<CategoryEntity>(content.categoryId);
            return result;
        }
    }
}
