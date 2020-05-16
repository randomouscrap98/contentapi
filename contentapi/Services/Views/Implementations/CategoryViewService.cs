using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{

    public class CategoryViewService : BasePermissionViewService<CategoryView, CategorySearch>
    {
        public CategoryViewService(ViewServicePack services, ILogger<CategoryViewService> logger, CategoryViewSource converter) 
            : base(services, logger, converter) { }

        public override string EntityType => Keys.CategoryType;
        public override string ParentType => Keys.CategoryType;

        public override async Task<EntityPackage> DeleteCheckAsync(long id, Requester requester)
        {
            var package = await base.DeleteCheckAsync(id, requester);
            FailUnlessSuper(requester); //Also only super users can delete
            return package;
        }
    }
}