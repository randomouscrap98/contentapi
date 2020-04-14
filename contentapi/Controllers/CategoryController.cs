using System;
using System.Collections.Generic;
using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public class CategorySearch : EntitySearchBase
    {
        public string Name {get;set;}
    }

    public class CategoryController : PermissionBaseController<CategoryView>
    {
        public CategoryController(ILogger<CategoryController> logger, ControllerServices services)
            : base(services, logger) 
        { 
        }

        protected override string EntityType => keys.CategoryType;
        
        protected override EntityPackage ConvertFromView(CategoryView view)
        {
            var package = NewEntity(view.name, view.description);
            package = BasicPermissionPackageSetup(package, view);
            return package;
        }

        protected override CategoryView ConvertToView(EntityPackage package)
        {
            var view = new CategoryView();
            view.name = package.Entity.name;
            view.description = package.Entity.content;
            view = BasicPermissionViewSetup(view, package);
            return view;
        }
    }
}