using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public abstract class BasePermissionViewService<V,S> : BaseEntityViewService<V,S> where V : StandardView where S : BaseSearch, new()
    {
        public BasePermissionViewService(ViewServicePack services, ILogger<BasePermissionViewService<V, S>> logger, IViewSource<V,EntityPackage,EntityGroup,S> converter) 
            : base(services, logger, converter) { }

        public abstract string ParentType {get;}
        public virtual bool AllowOrphanPosts => false;

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

            //TODO: searching for users should be done by the user view service! why are all these services mixing together aaaaa
            var found = await provider.ApplyEntitySearch(
                provider.GetQueryable<Entity>(), 
                new EntitySearch() { TypeLike = Keys.UserType, Ids = userIds }).CountAsync();

            //Note: there is NO type checking. Is this safe? Do you want people to be able to set permissions for 
            //things that aren't users? What about the 0 id?
            if(found != userIds.Count)
                throw new BadRequestException("One or more permission users not found!");
        }

        public override async Task<V> CleanViewGeneralAsync(V view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);

            if(view.parentId > 0)
            {
                var parent = await provider.FindByIdAsync(view.parentId);

                if(parent == null)
                    throw new BadRequestException($"No parent with id {view.parentId}");

                if(!String.IsNullOrEmpty(ParentType) && !parent.Entity.type.StartsWith(ParentType))
                    throw new BadRequestException("Wrong parent type!");

                //Only for CREATING. This is a silly weird thing since this is the general cleanup...
                //Almost NOTHING requires cleanup specifically for create.
                if(view.id == 0 && !CanUser(requester, Keys.CreateAction, parent))
                    throw new AuthorizationException($"User cannot create entities in parent {view.parentId}");
            }
            else if (!AllowOrphanPosts)
            {
                //Only super users can create parentless entities... for now. This is a safety feature and may (never) be removed
                FailUnlessSuper(requester);
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
        public override async Task<V> CleanViewUpdateAsync(V view, EntityPackage existing, Requester requester)
        {
            view = await base.CleanViewUpdateAsync(view, existing, requester);

            if (!CanUser(requester, Keys.UpdateAction, existing))
                throw new AuthorizationException("User cannot update this entity");

            //Restore the permissions from the package, don't bother throwing an error.
            if(!services.permissions.IsSuper(requester) && existing.GetRelation(Keys.CreatorRelation).entityId1 != requester.userId)
            {
                var existingView = converter.ToView(existing);
                view.permissions = existingView.permissions; //services.permissions.ConvertRelationsToPerms(existing.Relations);
            }

            return view;
        }

        public async override Task<EntityPackage> DeleteCheckAsync(long standinId, Requester requester)
        {
            var result = await base.DeleteCheckAsync(standinId, requester);

            if(!CanUser(requester, Keys.DeleteAction, result))
                throw new AuthorizationException("No permission to delete");

            return result;
        }

        public virtual bool CanUser(Requester requester, string action, EntityPackage package)
        {
            return services.permissions.CanUser(requester, action, package);
        }

        public virtual Task<Dictionary<long, string>> ComputeMyPermsAsync(List<EntityPackage> content, Requester requester)
        {
            //This ensures they ALL have it or something.
            var result = content.ToDictionary(x => x.Entity.id, y => new StringBuilder());

            //A potential optimization: pre-include read somehow... build this into search.

            foreach(var c in content)
            {
                foreach(var action in Actions.ActionMap) //services.permissions.PermissionActionMap)
                {
                    if(CanUser(requester, action.Value, c))
                        result[c.Entity.id].Append(action.Key);
                }
            }

            return Task.FromResult(result.ToDictionary(x => x.Key, y => y.Value.ToString()));
        }

        public override async Task<List<V>> PreparedSearchAsync(S search, Requester requester)
        {
            var ids = converter.SearchIds(search, (q) => services.permissions.PermissionWhere(q, requester, Keys.ReadAction));
            var packages = await converter.RetrieveAsync(ids);
            var perms = await ComputeMyPermsAsync(packages, requester);
            return packages.Select(x =>
            {
                var v = converter.ToView(x);
                v.myPerms = perms[v.id]; //This could fail if perms dictionary gets messed up
                return v;
            }).ToList();
        }
    }
}
