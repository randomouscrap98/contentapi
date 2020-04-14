using System;
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

    public class CategoryController : EntityBaseController<CategoryView>
    {
        public CategoryController(ILogger<CategoryController> logger, ControllerServices services)
            : base(services, logger) { }

        protected override string EntityType => keys.TypeCategory;

        protected override EntityPackage ConvertFromView(CategoryView view)
        {
            throw new NotImplementedException();
        }

        protected override CategoryView ConvertToView(EntityPackage package)
        {
            throw new NotImplementedException();
        }

        //protected override CategoryView GetViewFromExpanded(EntityPackage user)
        //{
        //    throw new System.NotImplementedException();
        //}
    }
}