using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    /// <summary>
    /// A permissive entity has permissions and a parent, essentially. Also assumed to be historic
    /// (why WOULDN'T you want to keep track of permission changes?!)
    /// </summary>
    public class BasePermissionViewConverter : BaseHistoricViewConverter
    {
        public List<EntityRelation> ConvertPermsToRelations(Dictionary<string, string> perms)
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

        public Dictionary<string, string> ConvertRelationsToPerms(IEnumerable<EntityRelation> relations)
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

        public List<EntityValue> FromViewValues(Dictionary<string,string> values)
        {
            var result = new List<EntityValue>();

            foreach(var v in values)
            {
                result.Add(new EntityValue()
                {
                    key = Keys.AssociatedValueKey + v.Key,
                    createDate = null,
                    value = v.Value
                });
            }

            return result;
        }

        public Dictionary<string,string> ToViewValues(IEnumerable<EntityValue> values)
        {
            var result = new Dictionary<string, string>();

            foreach(var v in values.Where(x => x.key.StartsWith(Keys.AssociatedValueKey)))
                result.Add(v.key.Substring(Keys.AssociatedValueKey.Length), v.value);

            return result;
        }

        public void ApplyToViewPermissive(EntityPackage package, BasePermissionView view)
        {
            ApplyToViewHistoric(package, view);

            if(package.HasRelation(Keys.ParentRelation))
                view.parentId = package.GetRelation(Keys.ParentRelation).entityId1;

            view.permissions = ConvertRelationsToPerms(package.Relations);
            view.values = ToViewValues(package.Values);
        }

        public void ApplyFromViewPermissive(BasePermissionView view, EntityPackage package, string type)
        {
            ApplyFromViewHistoric(view, package, type);

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

            FromViewValues(view.values).ForEach(x => 
            {
                x.entityId = view.id;
                package.Add(x);
            });
            
        }
    }
}