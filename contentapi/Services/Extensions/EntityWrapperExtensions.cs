using System;
using System.Linq;
using contentapi.Models;
using Randomous.EntitySystem;

namespace contentapi.Services.Extensions
{
    public static class EntityWrapperExtensions
    {
        /// <summary>
        /// Get an easy preformated EntityValue
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static EntityValue QuickValue(string key, string value)
        {
            return new EntityValue()
            {
                createDate = DateTime.Now,
                key = key,
                value = value
            };
        }

        /// <summary>
        /// Get an easy preformated EntityRelation
        /// </summary>
        /// <param name="entity1"></param>
        /// <param name="entity2"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static EntityRelation QuickRelation(long entity1, long entity2, string type, string value = null)
        {
            return new EntityRelation()
            {
                createDate = DateTime.Now,
                entityId1 = entity1,
                entityId2 = entity2,
                type = type,
                value = value
            };
        }

        /// <summary>
        /// Get an easy preformated entity
        /// </summary>
        /// <param name="name"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static EntityWrapper QuickEntity(string name, string content = null)
        {
            return new EntityWrapper()
            {
                createDate = DateTime.Now,
                name = name,
                content = content
            };
        }

        public static EntityWrapper AddValue(this EntityWrapper entity, string key, string value)
        {
            entity.Values.Add(QuickValue(key, value));
            return entity;
        }

        public static EntityWrapper AddRelation(this EntityWrapper entity, long entity1, long entity2, string type, string value = null)
        {
            entity.Relations.Add(QuickRelation(entity1, entity2, type, value));
            return entity;
        }

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