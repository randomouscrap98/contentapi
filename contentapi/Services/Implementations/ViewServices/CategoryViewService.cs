using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
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