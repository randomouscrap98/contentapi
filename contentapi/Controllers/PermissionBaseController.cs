using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        protected List<EntityRelation> ConvertPermsToRelations(Dictionary<string, string> perms)
        {
            var result = new List<EntityRelation>();
            foreach(var perm in perms)
            {
                foreach(var p in perm.Value.ToLower().Distinct().Select(x => x.ToString()))
                {
                    if(!permMapping.ContainsKey(p))
                        throw new InvalidOperationException("Bad character in permission");
                    
                    long userId = 0;

                    if(!long.TryParse(perm.Key, out userId))
                        throw new InvalidOperationException("Id not an integer!");

                    result.Add(NewRelation(userId, permMapping[p]));
                }
            }
            return result;
        }

        protected Dictionary<string, string> ConvertRelationsToPerms(IEnumerable<EntityRelation> relations)
        {
            var result = new Dictionary<string, string>();
            foreach(var relation in relations)
            {
                var perm = permMapping.Where(x => x.Value == relation.type);
                if(perm.Count() != 1)
                    continue;
                var userKey = relation.entityId1.ToString();
                if(!result.ContainsKey(userKey))
                    result.Add(userKey, "");
                result[userKey] += (perm.First().Key);
            }
            return result;
        }

        protected override EntityPackage BasicPackageSetup(EntityPackage package, V view)
        {
            base.BasicPackageSetup(package, view);

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

        protected override V BasicViewSetup(V view, EntityPackage package)
        {
            base.BasicViewSetup(view, package);

            //Set the creator to whatever the relation is
            view.userId = package.GetRelation(keys.CreatorRelation).entityId1;

            if(package.HasRelation(keys.ParentRelation))
                view.parentId = package.GetRelation(keys.ParentRelation).entityId1;

            view.permissions = ConvertRelationsToPerms(package.Relations);

            return view;
        }

        //protected async Task<V> PostCleanAsync(V view)
        //{
        //    //First, if view is "not new", go get the old and override values people can't change.
        //    if(view.id > 0)
        //    {
        //        var 
        //    }
        //    else
        //    {
        //        //if the view IS new, set the create date and uid to special values
        //        view.userId = GetRequesterUid();
        //        view.createDate = DateTime.Now;
        //    }
        //}
    }
}
