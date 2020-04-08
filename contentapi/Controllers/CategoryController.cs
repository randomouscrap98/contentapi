using AutoMapper;
using contentapi.Models;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class CategorySearch : EntitySearchBase
    {
        public string Name {get;set;}
    }

    public class CategoryController : EntityBaseController<CategoryView>
    {
        public CategoryController(ILogger<CategoryController> logger, IEntityProvider provider, IMapper mapper) 
            : base(logger, provider, mapper)
        {

        }

        protected override CategoryView GetViewFromExpanded(EntityWrapper user)
        {
            throw new System.NotImplementedException();
        }
    }
}