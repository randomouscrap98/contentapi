using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class CategorySearch : EntitySearchBase
    {
        public string Name {get;set;}
    }

    public class CategoryController : ProviderBaseController<CategoryController>
    {
        public CategoryController(ControllerServices<CategoryController> services)
            : base(services)
        {

        }

        //protected override CategoryView GetViewFromExpanded(EntityPackage user)
        //{
        //    throw new System.NotImplementedException();
        //}
    }
}