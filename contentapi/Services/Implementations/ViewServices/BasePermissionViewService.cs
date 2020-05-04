using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public abstract class BasePermissionViewService<V,S> : BaseEntityViewService<V,S> where V : BasePermissionView where S : EntitySearchBase, new()
    {
        public BasePermissionViewService(ViewServices services, ILogger<BasePermissionViewService<V, S>> logger) 
            : base(services, logger) { }

        public abstract string ParentType {get;}
        public virtual bool AllowOrphanPosts => false;

        public override EntityPackage ConvertFromView(V view)
        {
            var package = base.ConvertFromView(view);

            //There doesn't HAVE to be a parent
            if(view.parentId > 0)
                package.Add(NewRelation(view.parentId, keys.ParentRelation));
            
            //Now set up all the permission relations
            services.permissions.ConvertPermsToRelations(view.permissions).ForEach(x => 
            {
                x.createDate = null; //Don't store create date!
                package.Add(x);
            });

            //Done!
            return package;
        }

        public override V ConvertToView(EntityPackage package)
        {
            var view = base.ConvertToView(package);

            if(package.HasRelation(keys.ParentRelation))
                view.parentId = package.GetRelation(keys.ParentRelation).entityId1;

            view.permissions = services.permissions.ConvertRelationsToPerms(package.Relations);

            return view;
        }

        public async Task CheckPermissionUsersAsync(V view)
        {
            //And now make sure every single user exists
            var userIds = new List<long>();

            foreach(var perm in view.permissions)
            {
                long uid = 0;

                if(!long.TryParse(perm.Key, out uid))
                    throw new BadRequestException($"Cannot parse permission uid {perm.Key}");
                
                userIds.Add(uid);
            }

            userIds = userIds.Distinct().ToList();
            userIds.Remove(0); //Don't include the default

            //There are no more permissions.
            if(userIds.Count == 0)
                return;

            var found = await provider.ApplyEntitySearch(
                provider.GetQueryable<Entity>(), 
                new EntitySearch() { TypeLike = keys.UserType, Ids = userIds }).CountAsync();

            //Note: there is NO type checking. Is this safe? Do you want people to be able to set permissions for 
            //things that aren't users? What about the 0 id?
            if(found != userIds.Count)
                throw new BadRequestException("One or more permission users not found!");
        }

        public override async Task<V> CleanViewGeneralAsync(V view, long userId)
        {
            view = await base.CleanViewGeneralAsync(view, userId);

            if(view.parentId > 0)
            {
                var parent = await provider.FindByIdAsync(view.parentId);

                if(parent == null)
                    throw new BadRequestException($"No parent with id {view.id}");

                if(!String.IsNullOrEmpty(ParentType) && !parent.Entity.type.StartsWith(ParentType))
                    throw new BadRequestException("Wrong parent type!");

                //Only for CREATING. This is a silly weird thing since this is the general cleanup...
                //Almost NOTHING requires cleanup specifically for create.
                if(view.id == 0 && !services.permissions.CanUser(userId, keys.CreateAction, parent))
                    throw new BadRequestException($"User cannot create entities in parent {view.parentId}");
            }
            else if (!AllowOrphanPosts)
            {
                //Only super users can create parentless entities... for now. This is a safety feature and may (never) be removed
                FailUnlessSuper(userId);
            }

            await CheckPermissionUsersAsync(view);
            services.permissions.CheckPermissionValues(view.permissions);

            return view;
        }

        /// <summary>
        /// Do this on post update
        /// </summary>
        /// <param name="view"></param>
        /// <param name="standin"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        public override async Task<V> CleanViewUpdateAsync(V view, EntityPackage existing, long userId)
        {
            view = await base.CleanViewUpdateAsync(view, existing, userId);

            if (!services.permissions.CanUser(userId, keys.UpdateAction, existing))
                throw new AuthorizationException("User cannot update this entity");

            //Restore the permissions from the package, don't bother throwing an error.
            if(!services.permissions.IsSuper(userId) && existing.GetRelation(keys.CreatorRelation).entityId1 != userId)
                view.permissions = services.permissions.ConvertRelationsToPerms(existing.Relations);

            return view;
        }

        public async override Task<EntityPackage> DeleteCheckAsync(long standinId, long userId)
        {
            var result = await base.DeleteCheckAsync(standinId, userId);

            if(!services.permissions.CanUser(userId, keys.DeleteAction, result))
                throw new InvalidOperationException("No permission to delete");

            return result;
        }

        public virtual Task<Dictionary<long, string>> ComputeMyPermsAsync(List<EntityPackage> content, long userId)
        {
            //This ensures they ALL have it or something.
            var result = content.ToDictionary(x => x.Entity.id, y => new StringBuilder());

            //A potential optimization: pre-include read somehow... build this into search.

            foreach(var c in content)
            {
                foreach(var action in services.permissions.PermissionActionMap)
                {
                    if(services.permissions.CanUser(userId, action.Value, c))
                        result[c.Entity.id].Append(action.Key);
                }
            }

            return Task.FromResult(result.ToDictionary(x => x.Key, y => y.Value.ToString()));
        }

        public async Task<List<V>> ViewResult(IQueryable<Entity> query, long userId)
        {
            var packages = await provider.LinkAsync(query);
            var perms = await ComputeMyPermsAsync(packages, userId);
            return packages.Select(x => 
            {
                var v = ConvertToView(x);
                v.myPerms = perms[v.id]; //This could fail if perms dictionary gets messed up
                return v;
            }).ToList();
        }
    }
}
