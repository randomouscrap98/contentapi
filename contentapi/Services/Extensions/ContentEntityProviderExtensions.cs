using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Extensions
{
    public static class ContentEntityProviderExtensions
    {
        public static async Task<T> FindByIdAsyncGeneric<T>(this IEntityProvider provider, long id, Func<EntitySearch, Task<List<T>>> searcher)
        {
            var search = new EntitySearch();
            search.Ids.Add(id);
            return (await searcher(search)).OnlySingle();
        }

        /// <summary>
        /// Find some entity by id 
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static Task<EntityPackage> FindByIdAsync(this IEntityProvider provider, long id)
        {
            return provider.FindByIdAsyncGeneric(id, provider.GetEntityPackagesAsync);
        }

        public static async Task<EntityRelation> FindRelationByIdAsync(this IEntityProvider provider, long id)
        {
            var search = new EntityRelationSearch();
            search.Ids.Add(id);
            return (await provider.GetEntityRelationsAsync(search)).OnlySingle();
        }

        /// <summary>
        /// Find some entity by id 
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static Task<Entity> FindByIdBaseAsync(this IEntityProvider provider, long id)
        {
            return provider.FindByIdAsyncGeneric(id, provider.GetEntitiesAsync);
        }

        public static Task DeleteAsync(this IEntityProvider provider, EntityPackage package)
        {
            var deletes = new List<EntityBase>();
            deletes.Add(package.Entity);
            deletes.AddRange(package.Values);
            deletes.AddRange(package.Relations);
            return provider.DeleteAsync(deletes.ToArray());
        }
    }
}