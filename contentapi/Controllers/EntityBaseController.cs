using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public abstract class EntityBaseController<V> : SimpleBaseController where V : BaseView
    {
        public EntityBaseController(ControllerServices services, ILogger<EntityBaseController<V>> logger)
            :base(services, logger) { }

        protected abstract string EntityType {get;}

        /// <summary>
        /// Create a view with ONLY the unique fields for your controller filled in. You could fill in the
        /// others I guess, but they will be overwritten
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        protected abstract V CreateBaseView(EntityPackage package);

        /// <summary>
        /// Create a package with ONLY the unique fields for your controller filled in. 
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        protected abstract EntityPackage CreateBasePackage(V view);

        protected virtual V ConvertToView(EntityPackage package)
        {
            var view = CreateBaseView(package);

            //We are able to pull both the edit and create because all the info is in the package. we can't
            //go the other way (see above) because the view doesn't necessarily have the data we need.
            var creatorRelation = package.GetRelation(keys.CreatorRelation);
            view.createDate = (DateTime)package.Entity.createDateProper();
            view.editDate = (DateTime)
            view.id = package.Entity.id;

            return view;
        }

        //TRUST the view. Assume it is written correctly, that createdate is set properly, etc.
        protected virtual EntityPackage ConvertFromView(V view)
        {
            var package = CreateBasePackage(view);
            package.Entity.id = 0; //History dictates this must be 0, all entities for ANY view are new
            package.Entity.type = TypeSet(package.Entity.type, EntityType); //Steal the type directly from whatever they created
            package.Entity.createDate = view.createDate; //trust the create date from the view.

            //The standin pointer gets the real edit date.
            var relation = NewRelation(view.id, keys.StandInRelation);
            relation.createDate = view.editDate;
            package.Add(relation);

            return package;
        }


        /// <summary>
        /// Create the "base" entity that serves as a parent to all actual content. It allows the 
        /// special history system to function.
        /// </summary>
        /// <returns></returns>
        protected virtual async Task<EntityPackage> CreateStandInAsync()
        {
            //Create date is now and will never be changed
            var standin = NewEntity(null, keys.ActiveValue); 
            standin.Entity.type = keys.StandInType;

            //Link in the current user as the creator. Just a fun little thing. Allow the date here just in case it's needed
            var userId = GetRequesterUidNoFail();

            if(userId > 0)
            {
                //Don't need a datetime here right now. Hopefully we won't need it later... you can always remove it,
                //but you can't add it back in.... let's hope this isn't awful. If you need date, link into standin to get it.
                //They are created at the same time.
                var userLink = NewRelation(userId, keys.CreatorRelation);
                standin.Add(userLink);
            }
            else
            {
                logger.LogWarning("No user logged in while creating standin. It may just be a new user."); 
            }

            await services.provider.WriteAsync(standin);

            return standin;
        }

        protected async Task<EntityPackage> GetStandInAsync(long id)
        {
            var standin = await services.provider.FindByIdAsync(id); //go find the standin

            if(standin == null || !TypeIs(standin.Entity.type, keys.StandInType))
                throw new InvalidOperationException($"No entity with id {id}");
            
            return standin;
        }

        /// <summary>
        /// Mark the currently active entities/relations etc for the given standin as inactive. Return a copy
        /// of the objects as they were before being edited (for rollback purposes)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<List<EntityBase>> MarkLatestInactive(long standinId, string subValue = null)
        {
            var restoreCopies = new List<EntityBase>();

            //Go find the "previous" active content and relation. Ensure they are null if there are none (it should be ok, just throw a warning)
            var lastActiveRelation = (await GetActiveRelation(standinId)) ?? throw new InvalidOperationException("Could not find active relation in historic content system");
            var lastActiveContent = (await services.provider.FindByIdBaseAsync(lastActiveRelation.entityId2)) ?? throw new InvalidOperationException("Could not find active content in historic content system");

            //The state to restore should everything go south.
            restoreCopies.Add(new EntityRelation(lastActiveRelation));
            restoreCopies.Add(new Entity(lastActiveContent));

            //Mark the last content as historic (also set override sub-value if given, otherwise use what existed before)
            lastActiveRelation.value = TypeSet((subValue ?? TypeSub(lastActiveRelation.value, keys.ActiveValue)), keys.InactiveValue);
            lastActiveContent.type = TypeSet(lastActiveContent.type, keys.HistoryKey); //Prepend to the old type just to keep it around

            //Update old values FIRST so there's NO active content
            await services.provider.WriteAsync<EntityBase>(lastActiveContent, lastActiveRelation);

            return restoreCopies;
        }


        protected async Task<EntityPackage> WriteViewAsync(V view)
        {
            logger.LogTrace("WriteViewAsync called");

            var package = ConvertFromView(view); //Assume this does EVERYTHING

            //We assume the package was there.
            var standinRelation = package.GetRelation(keys.StandInRelation);
            EntityPackage standin = null; //This MAY be needed for some future stuff.
            bool newPackage = false;

            if(standinRelation.entityId1 == 0)
            {
                //Link in a new standin
                newPackage = true;
                logger.LogInformation("Creating standin for apparently new view");
                standin = await CreateStandInAsync();
                standinRelation.entityId1 = standin.Entity.id;
                standinRelation.value = TypeSet(keys.CreateAction, keys.ActiveValue); //standinRelation.value, keys.CreateAction); //This is new
            }
            else
            {
                standin = await GetStandInAsync(standinRelation.entityId1);
                standinRelation.value = TypeSet(keys.UpdateAction, keys.ActiveValue); //TypeSet(standinRelation.value, keys.UpdateAction); //This is an update
            }

            List<EntityBase> restoreCopies = new List<EntityBase>();

            //When it's NOT a new package, we have to go update historical records. Oof
            if(!newPackage)
                restoreCopies = await MarkLatestInactive(standinRelation.entityId1);

            try
            {
                //Write the new historic aware package
                await services.provider.WriteAsync(package);
            }
            catch
            {
                //Oh shoot something happened! get rid of the changes to the historic content (assumes write does not persist changes)
                await services.provider.WriteAsync(restoreCopies.ToArray());
                throw;
            }

            return package;
        }

        /// <summary>
        /// Allow "fake" deletion of ANY historic entity (of any type)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected Task DeleteEntity(long standinId)
        {
            return MarkLatestInactive(standinId, keys.DeleteAction);
        }

        /// <summary>
        /// Check the entity for deletion. Throw exception if can't
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async virtual Task<EntityPackage> DeleteEntityCheck(long standinId)
        {
            var last = await FindByIdAsync(standinId);

            if(last == null || !TypeIs(last.Entity.type, EntityType))
                throw new InvalidOperationException("No entity with that ID and type!");
            
            return last;
        }

        /// <summary>
        /// Find the relation that represents the current active content for the given standin 
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<EntityRelation> GetActiveRelation(long standinId)
        {
            var search = new EntityRelationSearch();
            search.EntityIds1.Add(standinId);
            search.TypeLike = keys.StandInRelation;
            var result = await services.provider.GetEntityRelationsAsync(search);
            return result.Where(x => TypeIs(x.value, keys.ActiveValue)).OnlySingle();
        }

        /// <summary>
        /// Modify a search converted from users so it works with real entities
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        protected async Task<EntitySearch> ModifySearchAsync(EntitySearch search)
        {
            //The easy modifications
            search = LimitSearch(search);

            if(string.IsNullOrWhiteSpace(search.TypeLike))
                search.TypeLike = "%";

            search.TypeLike = TypeSet(search.TypeLike, EntityType); 

            //We have to find the rEAL ids that they want. This is the only big thing...?
            if(search.Ids.Count > 0)
            {
                search.Ids = await ConvertStandInIdsAsync(search.Ids);

                if(search.Ids.Count == 0)
                    search.Ids.Add(long.MaxValue); //This should never be found, and should ensure nothing is found in the search
            }

            return search;
        }

        protected virtual V PostCleanUpdateAsync(V view, EntityPackage standin, EntityPackage existing)
        {
            view.createDate = (DateTime)standin.Entity.createDateProper();
            view.editDate = DateTime.UtcNow; //On update, edit is NOW (updating now etc idk)

            //Don't allow posting over some other entity! THIS IS SUUUUPER IMPORTANT!!!
            if(!TypeIs(existing.Entity.type, EntityType))
                throw new InvalidOperationException($"No entity of proper type with id {view.id}");
            
            return view;
        }

        protected virtual V PostCleanCreateAsync(V view)
        {
            //Create date should be NOOOWW
            view.createDate = DateTime.UtcNow;
            view.editDate = view.createDate; //Edit date should be EXACTLY the same as create date
            return view;
        }

        protected virtual async Task<V> PostCleanAsync(V view)
        {
            if(view.id > 0)
            {
                //This might be too heavy
                //This will already throw an exception if there isn't one.
                var standin = await GetStandInAsync(view.id);
                var existing = await FindByIdAsync(view.id);

                view = PostCleanUpdateAsync(view, await GetStandInAsync(view.id), await FindByIdAsync(view.id));
            }
            else
            {
                view = PostCleanCreateAsync(view);
            }

            return view;
        }

        protected async virtual Task<List<V>> ViewResult(IQueryable<Entity> query)
        {
            var packages = await services.provider.LinkAsync(query);
            return packages.Select(x => ConvertToView(x)).ToList();
        }
    }
}