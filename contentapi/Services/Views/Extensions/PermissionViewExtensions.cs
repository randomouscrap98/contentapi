using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Constants;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Extensions
{
    /// <summary>
    /// A permissive entity has permissions and a parent, essentially. Also assumed to be historic
    /// (why WOULDN'T you want to keep track of permission changes?!)
    /// </summary>
    public static class PermissionViewExtensions
    {
        public static List<EntityRelation> ConvertPermsToRelations(Dictionary<string, string> perms)
        {
            var result = new List<EntityRelation>();
            foreach(var perm in perms)
            {
                foreach(var p in perm.Value.ToLower().Distinct().Select(x => x.ToString()))
                {
                    if(!Actions.ActionMap.ContainsKey(p))
                        throw new InvalidOperationException("Bad character in permission");
                    
                    long userId = 0;

                    if(!long.TryParse(perm.Key, out userId))
                        throw new InvalidOperationException("Id not an integer!");

                    result.Add(new EntityRelation()
                    {
                        entityId1 = userId,
                        createDate = null,
                        type = Actions.ActionMap[p]
                    });
                }
            }
            return result;
        }

        public static Dictionary<string, string> ConvertRelationsToPerms(IEnumerable<EntityRelation> relations)
        {
            var result = new Dictionary<string, string>();
            foreach(var relation in relations)
            {
                var perm = Actions.ActionMap.Where(x => x.Value == relation.type);
                if(perm.Count() != 1)
                    continue;
                var userKey = relation.entityId1.ToString();
                if(!result.ContainsKey(userKey))
                    result.Add(userKey, "");
                result[userKey] += (perm.First().Key);
            }
            return result;
        }

        public static void ApplyToPermissionView<V,T>(this IViewConverter<V,T> converter, EntityPackage package, IPermissionView view)
        {
            if(package.HasRelation(Keys.ParentRelation))
                view.parentId = package.GetRelation(Keys.ParentRelation).entityId1;

            view.permissions = ConvertRelationsToPerms(package.Relations);
        }

        public static void ApplyFromPermissionView<V,T>(this IViewConverter<V,T> converter, IPermissionView view, EntityPackage package, string type)
        {
            //There doesn't HAVE to be a parent
            if(view.parentId > 0)
            {
                package.Add(new EntityRelation()
                {
                    entityId1 = view.parentId,
                    entityId2 = view.id,
                    type = Keys.ParentRelation,
                    createDate = null
                });
            }
            
            //Now set up all the permission relations
            ConvertPermsToRelations(view.permissions).ForEach(x => 
            {
                x.entityId2 = view.id;
                package.Add(x);
            });
        }
    }
}