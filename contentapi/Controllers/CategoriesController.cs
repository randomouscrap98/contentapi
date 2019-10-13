using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace contentapi.Controllers
{
    /*public class CategoriesController : AccessController<Category, CategoryView>
    {
        public CategoriesController(GenericControllerServices services, AccessService a):base(services, a) { }

        protected override void SetLogField(ActionLog log, long id) { log.categoryId = id; }

        //protected override IQueryable<Category> IncludeAccess(IQueryable<Category> query) { return query.Include(x => x.AccessList); }

        protected override async Task Post_PreInsertCheck(Category category)
        {
            await base.Post_PreInsertCheck(category);

            if(!await CanUserAsync(Permission.CreateCategory))
                ThrowAction(Unauthorized("No permission to create categories"));

            if(category.parentId != null)
            {
                var parentCategory = await services.context.GetSingleAsync<Category>((long)category.parentId);

                if(parentCategory == null)
                    ThrowAction(BadRequest("Nonexistent parent category!"));
            }
        }

        //protected override async Task<IQueryable<Category>> Get_GetBase()
        //{
        //    return (await base.Get_GetBase()).Include(x => x.AccessList);
        //}
    }*/
}