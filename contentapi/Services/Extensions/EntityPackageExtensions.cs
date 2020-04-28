using System.Collections.Generic;
using Randomous.EntitySystem;

namespace contentapi.Services.Extensions
{
    //Oops, some extensions I should've added in entitysystem
    public static class EntityPackageExtensions
    {
        /// <summary>
        /// Link all given values and relations to the given parent (do not write it!)
        /// </summary>
        /// <param name="values"></param>
        /// <param name="relations"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static void Relink(this Entity parent, IEnumerable<EntityValue> values, IEnumerable<EntityRelation> relations)
        {
            foreach(var v in values)
                v.entityId = parent.id;
            foreach(var r in relations)
                r.entityId2 = parent.id;
        }

        /// <summary>
        /// Assuming a valid base entity, relink all items in the package by id
        /// </summary>
        /// <param name="package"></param>
        public static void Relink(this EntityPackage package)
        {
            package.Entity.Relink(package.Values, package.Relations);
        }

        public static void FlattenPackage(this EntityPackage package, List<EntityBase> collection)
        {
            collection.AddRange(package.Values);
            collection.AddRange(package.Relations);
            collection.Add(package.Entity);
        }

        public static List<EntityBase> FlattenPackage(this EntityPackage package)
        {
            var result = new List<EntityBase>();
            FlattenPackage(package, result);
            return result;
        }
    }
}