using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public abstract class EntityBaseController<V> : ProviderBaseController where V : ViewBase
    {
        public EntityBaseController(ControllerServices services, ILogger<EntityBaseController<V>> logger)
            :base(services, logger) { }


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


        protected V ConvertToView(EntityPackage package)
        {
            var view = CreateBaseView(package);
            return BasicViewSetup(view, package);
        }

        protected EntityPackage ConvertFromView(V view)
        {
            var package = CreateBasePackage(view);
            package.Entity.type = TypeSet(package.Entity.type, EntityType); //Steal the type directly from whatever they created
            package = BasicPackageSetup(package, view);
            return package;
        }

        protected abstract string EntityType {get;}

        /// <summary>
        /// Create the "base" entity that serves as a parent to all actual content. It allows the 
        /// special history system to function.
        /// </summary>
        /// <returns></returns>
        protected async Task<long> CreateStandInAsync()
        {
            //Create date is now and will never be changed
            var standin = new Entity()
            {
                type = keys.StandInType,
                content = keys.ActiveIdentifier
            };

            await services.provider.WriteAsync(standin);

            return standin.id;
        }

        /// <summary>
        /// Find an entity by its STAND IN id (not the regular ID, use the service for that)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<EntityPackage> FindByIdAsync(long standinId)
        {
            var realIds = await ConvertStandInIdsAsync(standinId);

            if(realIds.Count() == 0)
                return null;
            else if(realIds.Count() > 1)
                throw new InvalidOperationException("Multiple entities for given standin, are there trailing history elements?");
            
            return await services.provider.FindByIdAsync(realIds.First());
        }

        /// <summary>
        /// Mark the currently active entities/relations etc for the given standin as inactive. Return a copy
        /// of the objects as they were before being edited (for rollback purposes)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<List<EntityBase>> MarkLatestInactive(long standinId)
        {
            var restoreCopies = new List<EntityBase>();

            //Go find the "previous" active content and relation. Ensure they are null if there are none (it should be ok, just throw a warning)
            var lastActiveRelation = (await GetActiveRelation(standinId)) ?? throw new InvalidOperationException("Could not find active relation in historic content system");
            var lastActiveContent = (await services.provider.FindByIdBaseAsync(lastActiveRelation.entityId2)) ?? throw new InvalidOperationException("Could not find active content in historic content system");

            //The state to restore should everything go south.
            restoreCopies.Add(new EntityRelation(lastActiveRelation));
            restoreCopies.Add(new Entity(lastActiveContent));

            //Mark the last content as historic
            lastActiveRelation.value = ""; //This marks it inactive
            lastActiveContent.type = TypeSet(lastActiveContent.type, keys.HistoryKey); //Prepend to the old type just to keep it around

            //Update old values FIRST so there's NO active content
            await services.provider.WriteAsync<EntityBase>(lastActiveContent, lastActiveRelation);

            return restoreCopies;
        }


        protected async Task<EntityPackage> WriteViewAsync(V view)
        {
            logger.LogTrace("WriteViewAsync called");

            var package = ConvertFromView(view); //Assume this does EVERYTHING
            package.Entity.id = 0;
            package.Entity.createDate = DateTime.Now; //Because of history, we always want it now.

            //We assume the package was there.
            var standin = package.GetRelation(keys.StandInRelation);
            bool newPackage = false;

            if(standin.entityId1 == 0)
            {
                newPackage = true;
                logger.LogInformation("Creating standin for apparently new view");
                standin.entityId1 = await CreateStandInAsync();
            }
            else
            {
                var entity = await services.provider.FindByIdBaseAsync(standin.entityId1); //go find the standin

                if(!TypeIs(entity?.type, keys.StandInType))
                    throw new InvalidOperationException($"No entity with id {standin.entityId1}");
            }

            //Set the package up so it's ready for a historic write
            //package = SetupPackageForWrite(standinId, package);
            List<EntityBase> restoreCopies = new List<EntityBase>();

            //When it's NOT a new package, we have to go update historical records. Oof
            if(!newPackage)
                restoreCopies = await MarkLatestInactive(standin.entityId1);

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

        //Parameters are like reading: is x y
        protected bool TypeIs(string type, string expected)
        {
            if(type == null)
                return false;

            return type.StartsWith(expected);
        }

        //Parameters are like reading: set x to y
        protected string TypeSet(string existing, string type)
        {
            return type + (existing ?? "");
        }

        /// <summary>
        /// Allow "fake" deletion of ANY historic entity (of any type)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected Task DeleteEntity(long standinId)
        {
            return MarkLatestInactive(standinId);
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
            return result.Where(x => x.value == keys.ActiveIdentifier).OnlySingle();
        }

        /// <summary>
        /// Convert stand-in ids (from the users) to real ids (that I use for searching actual entities)
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        protected async Task<List<long>> ConvertStandInIdsAsync(List<long> ids)
        {
            //This bites me every time. I need to fix the entity search system.
            if(ids.Count == 0)
                return new List<long>();

            var realRelations = await services.provider.GetEntityRelationsAsync(new EntityRelationSearch()
            {
                EntityIds1 = ids,
                TypeLike = keys.StandInRelation
            });

            return realRelations.Where(x => x.value == keys.ActiveIdentifier).Select(x => x.entityId2).ToList();
        }

        protected Task<List<long>> ConvertStandInIdsAsync(params long[] ids)
        {
            return ConvertStandInIdsAsync(ids.ToList());
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
            search.TypeLike = TypeSet(search.TypeLike, EntityType); //(search.TypeLike ?? "" ) + EntityType;

            //We have to find the rEAL ids that they want. This is the only big thing...?
            if(search.Ids.Count > 0)
            {
                search.Ids = await ConvertStandInIdsAsync(search.Ids);

                if(search.Ids.Count == 0)
                    search.Ids.Add(long.MaxValue); //This should never be found, and should ensure nothing is found in the search
            }

            return search;
        }

        /// <summary>
        /// Fill basic package fields using existing view
        /// </summary>
        /// <param name="package"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        protected virtual EntityPackage BasicPackageSetup(EntityPackage package, V view)
        {
            //This should be JUST conversion, do not assume this is being setup for writing!
            package.Entity.createDate = view.createDate;
            package.Add(NewRelation(view.id, keys.StandInRelation, keys.ActiveIdentifier));

            return package;
        }

        /// <summary>
        /// Fill basic view fields using existing package
        /// </summary>
        /// <param name="view"></param>
        /// <param name="package"></param>
        /// <returns></returns>
        protected virtual V BasicViewSetup(V view, EntityPackage package)
        {
            //This should JUST be conversion, do not assume this is being setup for writing!
            view.createDate = package.Entity.createDate;

            if(!package.HasRelation(keys.StandInRelation))
                throw new InvalidOperationException("Package has no stand-in relation, it is not part of the history system!");

            view.id = package.GetRelation(keys.StandInRelation).entityId1; //Entity.id;

            return view;
        }

        protected virtual async Task<V> PostCleanAsync(V view)
        {
            if(view.id > 0)
            {
                var realIds = await ConvertStandInIdsAsync(view.id);

                if(realIds.Count() != 1)
                    throw new InvalidOperationException($"No existing entity with id {view.id}");
            }

            return view;
        }
    }
}