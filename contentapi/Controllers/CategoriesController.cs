using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace contentapi.Controllers
{
    public class CategoryQuery : CollectionQuery
    {
        public long? parentId {get;set;} = null;
    }

    public class CategoriesController : EntityController<CategoryEntity, CategoryView>
    {
        public CategoriesController(EntityControllerServices services):base(services) { }

        protected override async Task<CategoryEntity> Post_ConvertItemAsync(CategoryView category)
        {
            var item = await base.Post_ConvertItemAsync(category);

            if(!await CanUserAsync(Permission.CreateCategory))
                ThrowAction(Unauthorized("No permission to create categories"));

            //The parent is not REQUIRED so don't "check required" unless it is given
            if(category.parentId != null)
                await CheckRequiredParentReadAsync<CategoryEntity>((long)category.parentId);

            return item;
        }

        //To REPLACE an action, simply mark it as a non-action, then redefine the function with something... uhhh better?
        [NonAction]
        public async override Task<ActionResult<Dictionary<string, object>>> Get([FromQuery]CollectionQuery query) { return await base.Get(query); }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<Dictionary<string, object>>> Get([FromQuery]CategoryQuery query)
        {
            try
            {
                var getBase = await GetAllReadableAsync();

                //Limit content by category (if they give it)
                if(query.parentId != null)
                {
                    //STRANGE code but essentially all negatives are... well, null (meaning root categories)
                    if(query.parentId < 0)
                        query.parentId = null;

                    getBase = getBase.Where(x => x.parentId == query.parentId);
                }

                return await GenericGetActionAsync(getBase, query);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }
    }
}