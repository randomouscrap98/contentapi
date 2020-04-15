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


        public V ConvertToView(EntityPackage package)
        {
            var view = CreateBaseView(package);
            return BasicViewSetup(view, package);
        }

        public EntityPackage ConvertFromView(V view)
        {
            var package = CreateBasePackage(view);
            return BasicPackageSetup(package, view);
        }

        protected abstract string EntityType {get;}

        /// <summary>
        /// Create the "base" entity that serves as a parent to all actual content. It allows the 
        /// special history system to function.
        /// </summary>
        /// <returns></returns>
        protected async Task<long> CreateStandinAsync()
        {
            var standin = new Entity()
            {
                type = keys.StandInType,
                content = keys.ActiveIdentifier
            };

            await services.provider.WriteAsync(standin);
            return standin.id;
        }

        protected long GetRequesterUid()
        {
            //Look for the UID from the JWT 
            var id = User.FindFirstValue(services.keys.UserIdentifier);

            if(id == null)
                throw new InvalidOperationException("User not logged in!");
            
            return long.Parse(id);
        }

        //protected async Task<List<EntityPackage>> Search(object search)
        //{
        //    var entitySearch = (EntitySearch)(await ModifySearchAsync(services.mapper.Map<EntitySearch>(search)));
        //    return await services.provider.GetEntityPackagesAsync(entitySearch);//.ToList();
        //}

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


        protected async Task<EntityPackage> WriteViewAsync(V view)
        {
            logger.LogTrace("WriteViewAsync called");

            var package = ConvertFromView(view); //Assume this does EVERYTHING
            package.Entity.type = EntityType; //Some setup for writing. guess it doesn't do EVERYTHING
            package.Entity.id = 0;

            //We assume the package was there.
            var standin = package.GetRelation(keys.StandInRelation);
            bool newPackage = false;

            if(standin.entityId1 == 0)
            {
                newPackage = true;
                logger.LogInformation("Creating standin for apparently new view");
                standin.entityId1 = await CreateStandinAsync();
            }
            else
            {
                var entity = await services.provider.FindByIdBaseAsync(standin.entityId1); //go find the standin

                if(entity?.type != keys.StandInType)
                    throw new InvalidOperationException($"No entity with id {package.Entity.id}");
            }

            //Set the package up so it's ready for a historic write
            //package = SetupPackageForWrite(standinId, package);
            List<EntityBase> restoreCopies = new List<EntityBase>();

            //When it's NOT a new package, we have to go update historical records. Oof
            if(!newPackage)
            {
                //Go find the "previous" active content and relation. Ensure they are null if there are none (it should be ok, just throw a warning)
                var lastActiveRelation = (await GetActiveRelation(standin.entityId1)) ?? throw new InvalidOperationException("Could not find active relation in historic content system");
                var lastActiveContent = (await services.provider.FindByIdBaseAsync(lastActiveRelation.entityId2)) ?? throw new InvalidOperationException("Could not find active content in historic content system");

                //The state to restore should everything go south.
                restoreCopies.Add(new EntityRelation(lastActiveRelation));
                restoreCopies.Add(new Entity(lastActiveContent));

                //Mark the last content as historic
                lastActiveRelation.value = ""; //This marks it inactive
                lastActiveContent.type = keys.HistoryKey + lastActiveContent.type; //Prepend to the old type just to keep it around

                //Update old values FIRST so there's NO active content
                await services.provider.WriteAsync<EntityBase>(lastActiveContent, lastActiveRelation);
            }

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

        protected async Task<List<long>> ConvertStandInIdsAsync(List<long> ids)
        {
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

        protected async Task<EntitySearch> ModifySearchAsync(EntitySearch search)
        {
            //The easy modifications
            search = LimitSearch(search);
            search.TypeLike = (search.TypeLike ?? "" ) + EntityType;

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
    }
}