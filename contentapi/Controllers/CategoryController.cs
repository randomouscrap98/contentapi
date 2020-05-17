using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class CategoryController : BaseViewServiceController<CategoryViewService, CategoryView, CategorySearch>
    {
        public CategoryController(ILogger<BaseSimpleController> logger, CategoryViewService service) 
            : base(logger, service) { }
    }
}