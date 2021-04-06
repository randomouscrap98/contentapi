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
        public CategoryViewService(ViewServicePack services, ILogger<CategoryViewService> logger, CategoryViewSource converter, BanViewSource banSource) 
            : base(services, logger, converter, banSource) { }

        public override string EntityType => Keys.CategoryType;
        public override string ParentType => Keys.CategoryType;
        public CategoryViewSource viewSource => (CategoryViewSource)converter;

        //Cache is static? mmmm should probably be a service, fix this later.
        protected static readonly object CacheLock = new object();
        protected static Dictionary<string, List<CategoryView>> Cache = new Dictionary<string, List<CategoryView>>();

        public override async Task<EntityPackage> DeleteCheckAsync(long id, Requester requester)
        {
            var package = await base.DeleteCheckAsync(id, requester);
            FailUnlessSuper(requester); //Also only super users can delete
            return package;
        }

        protected void FlushCache()
        {
            lock(CacheLock)
            {
                logger.LogInformation($"Flushing entire cateogry cache ({Cache.Count} cached)");
                Cache.Clear();
            }
        }

        //Track writes and deletes. ANY write/delete causes us to flush the full in-memory 
        public override async Task<CategoryView> WriteAsync(CategoryView view, Requester requester)
        {
            var result = await base.WriteAsync(view, requester);
            FlushCache();
            return result;
        }

        public override async Task<CategoryView> DeleteAsync(long entityId, Requester requester)
        {
            var result = await base.DeleteAsync(entityId, requester);
            FlushCache();
            return result;
        }

        public override async Task<List<CategoryView>> PreparedSearchAsync(CategorySearch search, Requester requester)
        {
            string key = JsonSerializer.Serialize(search) + JsonSerializer.Serialize(requester); 

            lock(CacheLock)
            {
                if(Cache.ContainsKey(key))
                {
                    logger.LogDebug($"Using cached category result ({Cache[key].Count}) for key {key}");
                    return Cache[key];
                }
            }

            var baseResult = await base.PreparedSearchAsync(search, requester);

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

            lock(CacheLock)
            {
                if(!Cache.ContainsKey(key))
                {
                    logger.LogDebug($"Caching {baseResult.Count} categories for key ({Cache.Count} total): {key}");
                    Cache.Add(key, baseResult);
                }
            }

            return baseResult;
        }
    }
}