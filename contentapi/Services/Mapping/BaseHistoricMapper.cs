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
    }
}