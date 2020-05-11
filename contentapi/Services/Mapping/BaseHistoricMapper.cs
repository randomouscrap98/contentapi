using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Mapping
{
    /// <summary>
    /// Slightly more complex mapper: historic entities have creators and editors 
    /// </summary>
    public class BaseHistoricMapper : BaseMapper
    {
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

        public void ApplyToViewHistoric(EntityPackage package, BaseEntityView view)
        {
            ApplyToViewBasic(package.Entity, view);

            //History has a creator and an editor. The create date comes from base
            var creatorRelation = package.GetRelation(Keys.CreatorRelation);

            view.editDate = (DateTime)creatorRelation.createDateProper();
            view.createUserId = creatorRelation.entityId1;
            view.editUserId = long.Parse(creatorRelation.value);
        }

        public void ApplyFromViewHistoric(BaseEntityView view, EntityPackage package, string type)
        {
            ApplyFromViewBasic(view, package.Entity);

            package.Entity.type = type + (package.Entity.type ?? "");

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
    }
}