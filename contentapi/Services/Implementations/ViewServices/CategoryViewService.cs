using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class CategoryViewService : BasePermissionViewService<CategoryView, CategorySearch>
    {
        public CategoryViewService(ViewServicePack services, ILogger<CategoryViewService> logger, CategoryViewSource converter, BanViewSource banSource) 
            : base(services, logger, converter, banSource) { }

        public override string EntityType => Keys.CategoryType;
        public override string ParentType => Keys.CategoryType;
        protected CategoryViewSource viewSource => (CategoryViewSource)converter;

        public override async Task<EntityPackage> DeleteCheckAsync(long id, Requester requester)
        {
            var package = await base.DeleteCheckAsync(id, requester);
            FailUnlessSuper(requester); //Also only super users can delete
            return package;
        }

        public override async Task<List<CategoryView>> PreparedSearchAsync(CategorySearch search, Requester requester)
        {
            var baseResult = await base.PreparedSearchAsync(search, requester);

            //var baseIds = baseResult.Select(x => x.id).ToList();

            if(baseResult.Count > 0 && search.ComputeExtras)
            {
                var categories = await viewSource.SimpleSearchAsync(new CategorySearch());  //Just pull every dang category, whatever
                var supers = viewSource.GetAllSupers(categories);

                baseResult.ForEach(x =>
                {
                    var createKey = Actions.KeyMap[Keys.CreateAction];
                    if(!x.myPerms.ToLower().Contains(createKey) && supers.ContainsKey(x.id) && supers[x.id].Contains(requester.userId))
                        x.myPerms += createKey;
                });
            }

            return baseResult;
        }
    }
}