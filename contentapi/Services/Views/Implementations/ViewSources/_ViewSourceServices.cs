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
    /// A bunch of independent functions for general modification of various types of views.
    /// </summary>
    public class ViewSourceServices
    {
        // Basic creation stuff

        public EntityPackage NewEntity(string name, string content = null)
        {
            return new EntityPackage()
            {
                Entity = new Entity() { 
                    name = name, 
                    content = content ,
                    createDate = DateTime.UtcNow
                }
            };
        }

        public EntityValue NewValue(string key, string value)
        {
            return new EntityValue() 
            {
                key = key, 
                value = value, 
                createDate = null 
            };
        }

        public EntityRelation NewRelation(long parent, string type, string value = null)
        {
            return new EntityRelation()
            {
                entityId1 = parent,
                type = type,
                value = value,
                createDate = null
            };
        }


        // Base view applications

        public void ApplyToBaseView(EntityBase entityBase, IBaseView view)
        {
            view.id = entityBase.id;
            view.createDate = (DateTime)entityBase.createDateProper();
        }

        public void ApplyFromBaseView(IBaseView view, EntityBase entityBase)
        {
            entityBase.id = view.id;
            entityBase.createDate = view.createDate;
        }


        // Edit vview applications

        public void ApplyToEditView(EntityPackage package, IEditView view)
        {
            ApplyToBaseView(package.Entity, view);

            //History has a creator and an editor. The create date comes from base
            var creatorRelation = package.GetRelation(Keys.CreatorRelation);

            view.editDate = (DateTime)creatorRelation.createDateProper();
            view.createUserId = creatorRelation.entityId1;
            view.editUserId = long.Parse(creatorRelation.value);
        }

        public void ApplyFromEditView(IEditView view, EntityPackage package, string type)
        {
            ApplyFromBaseView(view, package.Entity);

            package.Entity.type = type;

            var relation = new EntityRelation()
            {
                entityId1 = view.createUserId,
                entityId2 = view.id,
                type = Keys.CreatorRelation,
                value = view.editUserId.ToString(),
                createDate = view.editDate
            };

            package.Add(relation);
        }


        //Permission  view applications
        
        public List<EntityRelation> FromPerms(Dictionary<string, string> perms)
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

                    result.Add(NewRelation(userId, Actions.ActionMap[p]));
                }
            }
            return result;
        }

        public Dictionary<string, string> ToPerms(IEnumerable<EntityRelation> relations)
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

        public void ApplyToPermissionView(EntityPackage package, IPermissionView view)
        {
            if(package.HasRelation(Keys.ParentRelation))
                view.parentId = package.GetRelation(Keys.ParentRelation).entityId1;

            view.permissions = ToPerms(package.Relations);
        }

        public void ApplyFromPermissionView(IPermissionView view, EntityPackage package, string type)
        {
            //There doesn't HAVE to be a parent
            if(view.parentId > 0)
            {
                var relation = NewRelation(view.parentId, Keys.ParentRelation);
                relation.entityId2 = view.id;
                package.Add(relation);
            }
            
            //Now set up all the permission relations
            FromPerms(view.permissions).ForEach(x => 
            {
                x.entityId2 = view.id;
                package.Add(x);
            });
        }


        // Value view 

        public List<EntityValue> FromViewValues(Dictionary<string,string> values)
        {
            var result = new List<EntityValue>();

            foreach(var v in values)
                result.Add(NewValue(Keys.AssociatedValueKey + v.Key, v.Value));

            return result;
        }

        public Dictionary<string,string> ToViewValues(IEnumerable<EntityValue> values)
        {
            var result = new Dictionary<string, string>();

            foreach(var v in values.Where(x => x.key.StartsWith(Keys.AssociatedValueKey)))
                result.Add(v.key.Substring(Keys.AssociatedValueKey.Length), v.value);

            return result;
        }

        public void ApplyToValueView(EntityPackage package, IValueView view)
        {
            view.values = ToViewValues(package.Values);
        }

        public void ApplyFromValueView(IValueView view, EntityPackage package, string type)
        {
            FromViewValues(view.values).ForEach(x => 
            {
                x.entityId = view.id;
                package.Add(x);
            });
        }


        // Standard view stuff

        public void ApplyFromStandard(StandardView view, EntityPackage package, string type)
        {
            ApplyFromEditView(view, package, type);
            ApplyFromPermissionView(view, package, type);
            ApplyFromValueView(view, package, type);
        }

        public void ApplyToStandard(EntityPackage package, StandardView view)
        {
            ApplyToEditView(package, view);
            ApplyToPermissionView(package, view);
            ApplyToValueView(package, view);
        }
    }
}