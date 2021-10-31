using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;

namespace contentapi.Controllers
{
    public class ContentController : BaseViewServiceController<ContentViewService, ContentView, ContentSearch>
    {
        public ContentController(BaseSimpleControllerServices services, ContentViewService service) 
            : base(services, service) { }

        protected override Task SetupAsync() { return service.SetupAsync(); }
    }
}