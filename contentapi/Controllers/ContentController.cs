using contentapi.Models;
using System.Threading.Tasks;
using contentapi.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace contentapi.Controllers
{
    /*public class ContentController : AccessController<Content, ContentView>
    {
        public ContentController(GenericControllerServices services, AccessService a) : base(services, a) { }

        protected override void SetLogField(ActionLog log, long id) { log.contentId = id; }

        //This is a BAD HACK but for now this is what we can do
        //protected override IQueryable<Content> IncludeAccess(IQueryable<Content> query) { return query.Include(x => x.AccessList); }
        
        protected override async Task Post_PreConversionCheck(ContentView content)
        {
            await base.Post_PreConversionCheck(content);

            //Completely ignore whatever UID they gave us.
            content.userId = services.session.GetCurrentUid();
        }

        protected override async Task Post_PreInsertCheck(Content content)
        {
            await base.Post_PreInsertCheck(content);

            var category = await services.context.GetSingleAsync<Category>((long)content.categoryId);

            if(category == null)
                ThrowAction(BadRequest("Must provide category for content!"));

            var user = await GetCurrentUserAsync();

            if(!accessService.CanCreate(category, user))
                ThrowAction(Unauthorized("Can't create content in this category!"));
        }
    }*/
}
