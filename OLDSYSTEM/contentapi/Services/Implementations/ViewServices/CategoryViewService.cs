using System.Collections.Generic;
using System.Text.Json;
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
        protected CacheService<string, List<CategoryView>> cache;

        public CategoryViewService(ViewServicePack services, ILogger<CategoryViewService> logger, CategoryViewSource converter, BanViewSource banSource,
            CacheService<string, List<CategoryView>> cache) 
            : base(services, logger, converter, banSource) 
        { 
            this.cache = cache;
        }

        public override string EntityType => Keys.CategoryType;
        public override string ParentType => Keys.CategoryType;
        public CategoryViewSource viewSource => (CategoryViewSource)converter;

        public override async Task<EntityPackage> DeleteCheckAsync(long id, Requester requester)
        {
            var package = await base.DeleteCheckAsync(id, requester);
            FailUnlessSuper(requester); //Also only super users can delete
            return package;
        }

        //Track writes and deletes. ANY write/delete causes us to flush the full in-memory 
        public override async Task<CategoryView> WriteAsync(CategoryView view, Requester requester)
        {
            cache.PurgeCache();
            var result = await base.WriteAsync(view, requester);
            return result;
        }

        public override async Task<CategoryView> DeleteAsync(long entityId, Requester requester)
        {
            cache.PurgeCache();
            var result = await base.DeleteAsync(entityId, requester);
            return result;
        }

        public override async Task<List<CategoryView>> PreparedSearchAsync(CategorySearch search, Requester requester)
        {
            string key = JsonSerializer.Serialize(search) + JsonSerializer.Serialize(requester); 
            List<CategoryView> baseResult = null;

            if(cache.GetValue(key, ref baseResult))
                return baseResult;

            baseResult = await base.PreparedSearchAsync(search, requester);

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

            cache.StoreItem(key, baseResult);
            return baseResult;
        }
    }
}