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
    /// Slightly more complex mapper: historic entities have creators and editors 
    /// </summary>
    public static class EditViewExtensions
    {
        public static EntityPackage NewEntity<V,T>(this IViewConverter<V,T> converter, string name, string content = null)
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

        public static void ApplyToEditView<V,T>(this IViewConverter<V,T> converter, EntityPackage package, IEditView view)
        {
            converter.ApplyToBaseView(package.Entity, view);

            //History has a creator and an editor. The create date comes from base
            var creatorRelation = package.GetRelation(Keys.CreatorRelation);

            view.editDate = (DateTime)creatorRelation.createDateProper();
            view.createUserId = creatorRelation.entityId1;
            view.editUserId = long.Parse(creatorRelation.value);
        }

        public static void ApplyFromEditView<V,T>(this IViewConverter<V,T> converter, IEditView view, EntityPackage package, string type)
        {
            converter.ApplyFromBaseView(view, package.Entity);

            package.Entity.type = type; // + (package.Entity.type ?? "");

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