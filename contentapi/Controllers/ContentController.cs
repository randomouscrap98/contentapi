using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class ContentController : BaseViewServiceController<ContentViewService, ContentView, ContentSearch>
    {
        public ContentController(Keys keys, ILogger<BaseSimpleController> logger, ContentViewService service) 
            : base(keys, logger, service) { }
    }
}