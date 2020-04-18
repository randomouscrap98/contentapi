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
        public AuthorizationException(string message) : base(message) { }
    }

    public abstract class PermissionBaseController<V> : EntityBaseController<V> where V : PermissionView
    {
        protected Dictionary<string, string> permMapping;

        public PermissionBaseController(ControllerServices services, ILogger<EntityBaseController<V>> logger)
            :base(services, logger) 
        { 
            permMapping = new Dictionary<string, string>()
            {
                {"c", keys.CreateAction},
                {"r", keys.ReadAction},
                {"u", keys.UpdateAction},
                {"d", keys.DeleteAction}
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

        protected override EntityPackage ConvertFromView(V view)
        {
            var package = base.ConvertFromView(view);

            //The creator relation's parent is the editor, ie the one that is creating THIS entity package.
            //The creator relations's value is the original creator, which we keep around for posterity
            package.Add(NewRelation(view.editUserId, keys.CreatorRelation, view.userId.ToString()));

            //There doesn't HAVE to be a parent
            if(view.parentId > 0)
                package.Add(NewRelation(view.parentId, keys.ParentRelation));
            
            //Now set up all the permission relations
            ConvertPermsToRelations(view.permissions).ForEach(x => package.Add(x));

            //Done!
            return package;
        }

        protected override V ConvertToView(EntityPackage package)
        {
            var view = base.ConvertToView(package);

            //The creator of this REVISION is the editor, not the creator. the creator is the one
            //associated with the standin.
            var creator = package.GetRelation(keys.CreatorRelation);
            view.editUserId = creator.entityId1;
            view.userId = long.Parse(creator.value);

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

        /// <summary>
        /// Do this on post update
        /// </summary>
        /// <param name="view"></param>
        /// <param name="standin"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        protected override V PostCleanUpdateAsync(V view, EntityPackage standin, EntityPackage existing)
        {
            view = base.PostCleanUpdateAsync(view, standin, existing);

            view.userId = standin.GetRelation(keys.CreatorRelation).entityId1; //get it from the "horse's mouth" so to speak

            if (!CanCurrentUser(keys.UpdateAction, existing))
                throw new AuthorizationException("User cannot update this entity");

            return view;
        }

        /// <summary>
        /// Do this on post clean
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        protected override V PostCleanCreateAsync(V view)
        {
            view = base.PostCleanCreateAsync(view);
            //A new view? creator will always be us too
            view.userId = GetRequesterUid();
            return view;
        }

        protected override async Task<V> PostCleanAsync(V view)
        {
            view = await base.PostCleanAsync(view);

            //Editor is ALWAYS us, we're doing it
            view.editUserId = GetRequesterUid();

            //Oh also make sure the parent exists.
            if(view.parentId > 0)
            {
                var parent = await FindByIdAsync(view.parentId); //wait is this the standin? uhh yes always.

                if(parent == null)
                    throw new InvalidOperationException($"No parent with id {view.id}");

                if(!TypeIs(parent.Entity.type, ParentType))
                    throw new InvalidOperationException("Wrong parent type!");

                if(!CanCurrentUser(keys.CreateAction, parent))
                    throw new AuthorizationException($"User cannot create entities in parent {view.parentId}");
            }
            else
            {
                //Only super users can create parentless entities... for now. This is a safety feature and may (never) be removed
                FailUnlessRequestSuper();
            }

            await CheckPermissionUsersAsync(view);
            CheckPermissionValues(view);

            return view;
        }

        protected async override Task<EntityPackage> DeleteEntityCheck(long standinId)
        {
            var result = await base.DeleteEntityCheck(standinId);

            if(!CanCurrentUser(keys.DeleteAction, result))
                throw new InvalidOperationException("No permission to delete");

            return result;
        }

        protected IQueryable<EntityBase> ConvertToHusk(IQueryable<EntityREGroup> groups)
        {
            return 
                from x in groups
                group x by x.entity.id into g
                select new EntityBase() { id = g.Key };
        }

        //protected async override Task<List<V>> ViewResult(IEnumerable<EntityPackage> packages)
        //{
        //    var packagesStandin = packages.Select(x => new { package = x, standinId = x.GetRelation(keys.StandInRelation).entityId1 });

        //    var search = new EntityRelationSearch() { EntityIds2 = packagesStandin.Select(x => x.standinId).ToList() }; //packages.Select(x => x.GetRelation(keys.StandInRelation).entityId1).ToList() };
        //    var creators = await services.provider.GetEntityRelationsAsync(search);

        //    var linked = from p in packagesStandin
        //                 join c in creators on p.standinId equals c.entityId2 into g
        //                 select new { package = p.package, creator = g.First() };

        //    return linked.Select(x => 
        //    {
        //        var view = ConvertToView(x.package);
        //        view.userId = x.creator.entityId1;
        //        return view;
        //    }).ToList();
        //}

    }
}
