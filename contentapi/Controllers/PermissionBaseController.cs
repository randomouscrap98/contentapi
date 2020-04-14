using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public abstract class PermissionBaseController<V> : EntityBaseController<V> where V : PermissionView
    {
        protected Dictionary<string, string> permMapping;

        public PermissionBaseController(ControllerServices services, ILogger<EntityBaseController<V>> logger)
            :base(services, logger) 
        { 
            permMapping = new Dictionary<string, string>()
            {
                {"c", keys.CreateAccess},
                {"r", keys.ReadAccess},
                {"u", keys.UpdateAccess},
                {"d", keys.DeleteAccess}
            };
        }

        protected List<EntityRelation> ConvertPermsToRelations(Dictionary<long, string> perms)
        {
            var result = new List<EntityRelation>();
            foreach(var perm in perms)
            {
                foreach(var p in perm.Value.ToLower().Select(x => x.ToString()))
                {
                    if(!permMapping.ContainsKey(p))
                        throw new InvalidOperationException("Bad character in permission");

                    result.Add(NewRelation(perm.Key, permMapping[p]));
                }
            }
            return result;
        }

        protected Dictionary<long, string> ConvertRelationsToPerms(IEnumerable<EntityRelation> relations)
        {
            var result = new Dictionary<long, string>();
            foreach(var relation in relations)
            {
                var perm = permMapping.Where(x => x.Value == relation.type);
                if(perm.Count() != 1)
                    continue;
                if(!result.ContainsKey(relation.entityId1))
                    result.Add(relation.entityId1, "");
                result[relation.entityId1] += (perm.First().Key);
            }
            return result;
        }

        protected EntityPackage BasicPermissionPackageSetup(EntityPackage package, V view)
        {
            //There HAS to be a creator
            package.Add(NewRelation(view.userId, keys.CreatorRelation));

            //There doesn't HAVE to be a parent
            if(view.parentId > 0)
                package.Add(NewRelation(view.parentId, keys.ParentRelation));
            
            //Now set up all the permission relations
            ConvertPermsToRelations(view.permissions).ForEach(x => package.Add(x));

            //Done!
            return package;
        }

        protected V BasicPermissionViewSetup(V view, EntityPackage package)
        {
            //Set the creator to whatever the relation is
            view.userId = package.GetRelation(keys.CreatorRelation).entityId1;

            if(package.HasRelation(keys.ParentRelation))
                view.parentId = package.GetRelation(keys.ParentRelation).entityId1;

            view.permissions = ConvertRelationsToPerms(package.Relations);

            return view;
        }
    }
}
