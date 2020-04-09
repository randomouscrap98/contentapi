using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Models;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Extensions
{
    public static class EntityProviderWrapperExtensions
    {
        public static ILogger Logger = null;

        /// <summary>
        /// Write an entire entity wrapper as-is, setting all associated ids.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static async Task WriteAsync(this IEntityProvider provider, EntityWrapper entity)
        {
            Logger?.LogTrace($"WriteAsync called for entitywrapper {entity.name}");

            //First, write the entity. Then TRY to write everything else. If ANYTHING fails,
            //delete the entity.
            await provider.WriteAsync(entity);

            try
            {
                entity.Values.ForEach(x => x.entityId = entity.id);
                entity.Relations.ForEach(x => x.entityId2 = entity.id); //Assume relations are all parents. a user has perms ON this entity, a category OWNS this entity, etc.
                var allWrite = new List<EntityBase>();
                allWrite.AddRange(entity.Values);
                allWrite.AddRange(entity.Relations);
                await provider.WriteAsync(allWrite.ToArray());
            }
            catch
            {
                await provider.DeleteAsync(entity);
                throw;
            }
        }

        /// <summary>
        /// Link a queryable entity list with values/relations to produce a list of wrapped entities
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="queryable"></param>
        /// <returns></returns>
        public static async Task<List<EntityWrapper>> LinkAsync(this IEntityProvider provider, IQueryable<Entity> queryable)
        {
            Logger?.LogTrace("LinkAsync called on entity queryable");

            var bigGroup = from e in queryable //entityProvider.ApplyEntitySearch(entityProvider.GetQueryable<Entity>(), search)
                           join v in provider.GetQueryable<EntityValue>() on e.id equals v.entityId into evs
                           from v in evs.DefaultIfEmpty()
                           join r in provider.GetQueryable<EntityRelation>() on e.id equals r.entityId2 into evrs
                           from r in evrs.DefaultIfEmpty()
                           select new { Entity = e, Value = v, Relation = r};
            
            var grouping = (await provider.GetListAsync(bigGroup)).ToLookup(x => x.Entity.id);

            return grouping.Select(x => new EntityWrapper(x.First().Entity)
            {
                Values = x.Select(x => x.Value).ToList(),
                Relations = x.Select(x => x.Relation).ToList()
            }).ToList();
        }

        /// <summary>
        /// Search for entities, return them wrapped
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        public static Task<List<EntityWrapper>> SearchAsync(this IEntityProvider provider, EntitySearch search)
        {
            return LinkAsync(provider, provider.ApplyEntitySearch(provider.GetQueryable<Entity>(), search));
        }
    }
}