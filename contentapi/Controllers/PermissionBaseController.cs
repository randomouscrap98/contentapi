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
    public class AuthorizationException : Exception
    {
        public AuthorizationException() : base() {}
        public AuthorizationException(string message) : base(message) { } //, Exception inner) : base(message, inner) {}
    }

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

        protected abstract string ParentType {get;}

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

        protected async Task CheckPermissionUsersAsync(V view)
        {
            //And now make sure every single user exists
            var userIds = new List<long>();

            foreach(var perm in view.permissions)
            {
                long uid = 0;

                if(!long.TryParse(perm.Key, out uid))
                    throw new InvalidOperationException($"Cannot parse permission uid {perm.Key}");
                
                userIds.Add(uid);
            }

            userIds = userIds.Distinct().ToList();
            userIds.Remove(0); //Don't include the default
            var realIds = await ConvertStandInIdsAsync(userIds);

            //Note: there is NO type checking. Is this safe? Do you want people to be able to set permissions for 
            //things that aren't users? What about the 0 id?
            if(realIds.Count != userIds.Count)
                throw new InvalidOperationException("One or more permission users not found!");
        }

        protected void CheckPermissionValues(V view)
        {
            foreach(var perm in view.permissions)
            {
                if(perm.Value.ToLower().Any(x => !permMapping.Keys.Contains(x.ToString())))
                    throw new InvalidOperationException($"Invalid characters in permission: {perm.Value}");
            }
        }

        protected bool CanUser(long user, string key, EntityPackage package)
        {
            return (package.GetRelation(keys.CreatorRelation).entityId1 == user) ||
                (package.Relations.Any(x => x.type == key && (x.entityId1 == user || x.entityId1 == 0)));
        }

        protected bool CanCurrentUser(string key, EntityPackage package)
        {
            return CanUser(GetRequesterUidNoFail(), key, package);
        }

        protected override async Task<V> PostCleanAsync(V view)
        {
            view = await base.PostCleanAsync(view);

            //First, if view is "not new", go get the old and override values people can't change.
            if(view.id > 0)
            {
                //This might be too heavy
                var existing = await FindByIdAsync(view.id);

                if(!TypeIs(existing.Entity.type, EntityType))
                    throw new InvalidOperationException($"No entity of proper type with id {view.id}");
                
                //Don't allow the user to change these things.
                view.userId = existing.GetRelation(keys.CreatorRelation).entityId1;

                if(!CanCurrentUser(keys.UpdateAccess, existing))
                    throw new AuthorizationException("User cannot update this entity");
            }
            else
            {
                //if the view IS new, set the create date and uid to special values
                view.userId = GetRequesterUid();
            }

            //Oh also make sure the parent exists.
            if(view.parentId > 0)
            {
                var parent = await FindByIdAsync(view.parentId); //wait is this the standin? uhh yes always.

                if(parent == null)
                    throw new InvalidOperationException($"No parent with id {view.id}");

                if(!TypeIs(parent.Entity.type, ParentType))
                    throw new InvalidOperationException("Wrong parent type!");

                if(!CanCurrentUser(keys.CreateAccess, parent))
                    throw new AuthorizationException($"User cannot create entities in parent {view.parentId}");
            }
            else
            {
                //Only super users can create parentless entities... for now. This is a safety feature and may be removed
                FailUnlessRequestSuper();
            }

            await CheckPermissionUsersAsync(view);
            CheckPermissionValues(view);

            return view;
        }

        protected async override Task<EntityPackage> DeleteEntityCheck(long standinId)
        {
            var result = await base.DeleteEntityCheck(standinId);

            if(!CanCurrentUser(keys.DeleteAccess, result))
                throw new InvalidOperationException("No permission to delete");

            return result;
        }

        //protected IQueryable<EntityBase> BasicPermissionHusk(long user, EntitySearch search)
        //{
        //    return 
        //        from e in services.provider.ApplyEntitySearch(services.provider.GetQueryable<Entity>(), search, false)
        //        join r in services.provider.GetQueryable<EntityRelation>()
        //            on  e.id equals r.entityId2
        //        where (r.type == keys.CreatorRelation && r.entityId1 == user) ||
        //              (r.type == keys.ReadAccess && (r.entityId1 == 0 || r.entityId1 == user))
        //        group e by e.id into g
        //        select new EntityBase() { id = g.Key };
        //}

        protected class EntityRelationGroup
        {
            public Entity entity;
            public EntityRelation relation;
        }

        protected IQueryable<EntityRelationGroup> BasicPermissionQuery(long user, EntitySearch search)
        {
            return   
                services.provider.ApplyEntitySearch(services.provider.GetQueryable<Entity>(), search, false)
                .Join(services.provider.GetQueryable<EntityRelation>(), e => e.id, r => r.entityId2, (e,r) => new EntityRelationGroup() { entity = e, relation = r})
                .Where(x => (x.relation.type == keys.CreatorRelation && x.relation.entityId1 == user) ||
                      (x.relation.type == keys.ReadAccess && (x.relation.entityId1 == 0 || x.relation.entityId1 == user)));
        }

        protected IQueryable<EntityBase> ConvertToHusk(IQueryable<EntityRelationGroup> groups)
        {
            return 
                from x in groups
                group x by x.entity.id into g
                select new EntityBase() { id = g.Key };
        }

        /// <summary>
        /// Given a completed IQueryable, apply the final touches to get a real list of entities
        /// </summary>
        /// <param name="foundEntities"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        protected IQueryable<Entity> FinalizeHusk(IQueryable<EntityBase> foundEntities, EntitySearch search)
        {
            var ids = services.provider.ApplyFinal(foundEntities, search).Select(x => x.id);
            var join =
                from e in services.provider.GetQueryable<Entity>()
                where ids.Contains(e.id)
                select e;

            //This is REPEAT CODE! FIGURE OUT HOW TO FIX THIS! This is required because order is not preserved
            //after the "join" (the fake join using in-memory data oof)
            if(search.Reverse)
                join = join.OrderByDescending(x => x.id);
            else
                join = join.OrderBy(x => x.id);

            return join;
        }
            
    }
}
