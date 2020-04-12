using System.Threading.Tasks;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Extensions
{
    public static class ContentEntityProviderExtensions
    {
        /// <summary>
        /// Find a value by key/value/id (added constraints)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<EntityValue> FindValueAsync(this IEntityProvider entityProvider, string key, string value = null, long id = -1)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = key };
            if(value != null)
                valueSearch.ValueLike = value;
            if(id > 0)
                valueSearch.EntityIds.Add(id);
            return (await entityProvider.GetEntityValuesAsync(valueSearch)).OnlySingle();
        }

        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static async Task<EntityPackage> FindByNameAsync(this IEntityProvider provider, string name)
        {
            return (await provider.GetEntityPackagesAsync(new EntitySearch() {NameLike = name})).OnlySingle();
        }

        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static async Task<Entity> FindByNameBaseAsync(this IEntityProvider provider, string name)
        {
            return (await provider.GetEntitiesAsync(new EntitySearch() {NameLike = name})).OnlySingle();
        }

        /// <summary>
        /// Find some entity by id 
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static async Task<EntityPackage> FindByIdAsync(this IEntityProvider provider, long id)
        {
            var search = new EntitySearch();
            search.Ids.Add(id);
            return (await provider.GetEntityPackagesAsync(search)).OnlySingle();
        }
    }
}