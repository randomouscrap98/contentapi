using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class CategoriesController : EntityController<CategoryEntity, CategoryView>
    {
        public CategoriesController(EntityControllerServices services):base(services) { }

        protected override async Task<CategoryEntity> Post_ConvertItemAsync(CategoryView category)
        {
            var item = await base.Post_ConvertItemAsync(category);

            if(!await CanUserAsync(Permission.CreateCategory))
                ThrowAction(Unauthorized("No permission to create categories"));

            if(category.parentId != null)
            {
                var parentCategory = await GetSingleBase((long)category.parentId);

                if(parentCategory == null)
                    ThrowAction(BadRequest("Nonexistent parent category!"));
            }

            return item;
        }
    }
}