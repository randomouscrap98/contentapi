using System;
using System.Linq;
using contentapi.Models;
using Randomous.EntitySystem;

namespace contentapi.Services.Extensions
{
    public static class EntityWrapperExtensions
    {
        /// <summary>
        /// Get a value from a wrapper
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetValue(this EntityWrapper entity, string key)
        {
            var values = entity.Values.Where(x => x.key == key);

            if(values.Count() != 1)
                throw new InvalidOperationException($"Not a single value for key: {key}");
            
            return values.First().value;
        }

        /// <summary>
        /// See if a wrapper has a value (not necessarily singular)
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool HasValue(this EntityWrapper entity, string key)
        {
            return entity.Values.Any(x => x.key == key);
        }

        /// <summary>
        /// Do everything to make this entity and all associated values look "new"
        /// </summary>
        /// <param name="entity"></param>
        public static void SetEntityAsNew(this EntityWrapper entity)
        {
            entity.id = 0;
            entity.Values.ForEach(x => x.id = 0);
            entity.Relations.ForEach(x => x.id = 0); //Assume relations are all parents. a user has perms ON this entity, a category OWNS this entity, etc.
        }
    }
}