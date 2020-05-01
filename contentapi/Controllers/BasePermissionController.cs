using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public abstract class BasePermissionController<V> : BaseEntityController<V> where V : BasePermissionView
    {

        public BasePermissionController(ControllerServices services, ILogger<BaseEntityController<V>> logger)
            :base(services, logger) 
        { 
        }

        protected abstract string ParentType {get;}
        protected virtual bool AllowOrphanPosts => false;

        protected override EntityPackage ConvertFromView(V view)
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

        protected override V ConvertToView(EntityPackage package)
        {
            var view = base.ConvertToView(package);

            if(package.HasRelation(keys.ParentRelation))
                view.parentId = package.GetRelation(keys.ParentRelation).entityId1;

            view.permissions = services.permissions.ConvertRelationsToPerms(package.Relations);

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

        protected override async Task<V> CleanViewGeneralAsync(V view)
        {
            view = await base.CleanViewGeneralAsync(view);

            if(view.parentId > 0)
            {
                var parent = await provider.FindByIdAsync(view.parentId);

                if(parent == null)
                    throw new BadRequestException($"No parent with id {view.id}");

                if(!String.IsNullOrEmpty(ParentType) && !parent.Entity.type.StartsWith(ParentType))
                    throw new BadRequestException("Wrong parent type!");

                //Only for CREATING. This is a silly weird thing since this is the general cleanup...
                //Almost NOTHING requires cleanup specifically for create.
                if(view.id == 0 && !CanCurrentUser(keys.CreateAction, parent))
                    throw new BadRequestException($"User cannot create entities in parent {view.parentId}");
            }
            else if (!AllowOrphanPosts)
            {
                //Only super users can create parentless entities... for now. This is a safety feature and may (never) be removed
                FailUnlessRequestSuper();
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
        protected override async Task<V> CleanViewUpdateAsync(V view, EntityPackage existing)
        {
            view = await base.CleanViewUpdateAsync(view, existing);

            if (!CanCurrentUser(keys.UpdateAction, existing))
                throw new AuthorizationException("User cannot update this entity");

            //Restore the permissions from the package, don't bother throwing an error.
            if(!services.systemConfig.SuperUsers.Contains(GetRequesterUidNoFail()) && existing.GetRelation(keys.CreatorRelation).entityId1 != GetRequesterUid())
                view.permissions = services.permissions.ConvertRelationsToPerms(existing.Relations);

            return view;
        }


        protected async override Task<EntityPackage> DeleteCheckAsync(long standinId)
        {
            var result = await base.DeleteCheckAsync(standinId);

            if(!CanCurrentUser(keys.DeleteAction, result))
                throw new InvalidOperationException("No permission to delete");

            return result;
        }

    }
}
