using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class CategoryController : BaseViewServiceController<CategoryViewService, CategoryView, CategorySearch>
    {
        public CategoryController(Keys keys, ILogger<BaseSimpleController> logger, CategoryViewService service) 
            : base(keys, logger, service) { }
    }
}