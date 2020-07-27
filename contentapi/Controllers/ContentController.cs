using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class ContentController : BaseViewServiceController<ContentViewService, ContentView, ContentSearch>
    {
        //public ContentController(ILogger<ContentController> logger, ContentViewService service) 
        //    : base(logger, service) { }
        public ContentController(BaseSimpleControllerServices services, ContentViewService service) 
            : base(services, service) { }

        protected override Task SetupAsync() { return service.SetupAsync(); }
    }
}