using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace contentapi.Controllers
{
    public class ContentQuery : CollectionQuery
    {
        public long? categoryId {get;set;} = null;
        public string type {get;set;} = null;
    }

    public class ContentController : EntityController<ContentEntity, ContentView>
    {
        public ContentController(EntityControllerServices services) : base(services) { }

        protected override async Task<ContentEntity> Post_ConvertItemAsync(ContentView content)
        {
            var result = await base.Post_ConvertItemAsync(content);
            await CheckRequiredParentReadAsync<CategoryEntity>(content.categoryId);
            return result;
        }

        //To REPLACE an action, simply mark it as a non-action, then redefine the function with something... uhhh better?
        [NonAction]
        public async override Task<ActionResult<Dictionary<string, object>>> Get([FromQuery]CollectionQuery query) { return await base.Get(query); }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<Dictionary<string, object>>> Get([FromQuery]ContentQuery query)
        {
            try
            {
                var getBase = await GetAllReadableAsync();

                //Limit content by category (if they give it)
                if(query.categoryId != null)
                    getBase = getBase.Where(x => x.categoryId == query.categoryId);
                if(query.type != null)
                    getBase = getBase.Where(x => x.type == query.type);

                return await GenericGetActionAsync(getBase, query);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }
    }
}
