using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class ContentController : BaseViewServiceController<ContentViewService, ContentView, ContentSearch>
    {
        public ContentController(ILogger<BaseSimpleController> logger, ContentViewService service) 
            : base(logger, service) { }
        
        protected override Task SetupAsync()
        {
            return service.SetupAsync();
        }
    }
}