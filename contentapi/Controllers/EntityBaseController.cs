using System;
using System.Collections.Generic;
using System.Linq;
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


        protected abstract V ConvertToView(EntityPackage user);
        protected abstract EntityPackage ConvertFromView(V view);
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

        protected async Task<EntityPackage> WriteViewAsync(V view)
        {
            logger.LogTrace("WriteViewAsync called");

            var package = ConvertFromView(view);
            package.Entity.type = EntityType;

            long standinId = -1;
            bool newPackage = false;

            if(package.Entity.id == 0)
            {
                newPackage = true;
                logger.LogInformation("Creating standin for apparently new view");
                standinId = await CreateStandinAsync();
            }
            else
            {
                var entity = await services.provider.FindByIdBaseAsync(package.Entity.id);

                if(entity?.type != keys.StandInType)
                    throw new InvalidOperationException($"No entity with id {package.Entity.id}");
                
                standinId = package.Entity.id;
            }

            //Set the package up so it's ready for a historic write
            package = SetupPackageForWrite(standinId, package);
            List<EntityBase> restoreCopies = new List<EntityBase>();

            //When it's NOT a new package, we have to go update historical records. Oof
            if(!newPackage)
            {
                //Go find the "previous" active content and relation. Ensure they are null if there are none (it should be ok, just throw a warning)
                var lastActiveRelation = (await GetActiveRelation(standinId)) ?? throw new InvalidOperationException("Could not find active relation in historic content system");
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

            return SetupPackageForRead(package);
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
        /// Modify a package so it is written as historic
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        protected EntityPackage SetupPackageForWrite(long standinKey, EntityPackage package)
        {
            if(package.HasRelation(keys.StandInRelation))
                throw new InvalidOperationException("Package already has standin key! Don't add this yourself!");
            
            package.Add(NewRelation(standinKey, keys.StandInRelation, keys.ActiveIdentifier));
            package.Entity.id = 0;
            return package;
        }

        /// <summary>
        /// Modify a package so it can be read transparently (without knowledge of the history system)
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        protected EntityPackage SetupPackageForRead(EntityPackage package)
        {
            if(!package.HasRelation(keys.StandInRelation))
                throw new InvalidOperationException("Package has no standin! What package is this?");
            
            package.Entity.id = package.GetRelation(keys.StandInRelation).entityId1; //the actual standin entity.
            
            return package;
        }

        protected async Task<EntitySearch> ModifySearchAsync(EntitySearch search)
        {
            //The easy modifications
            search = LimitSearch(search);
            search.TypeLike = (search.TypeLike ?? "" ) + EntityType;

            //We have to find the rEAL ids that they want. This is the only big thing...?
            if(search.Ids.Count > 0)
            {
                var realRelations = await services.provider.GetEntityRelationsAsync(new EntityRelationSearch()
                {
                    EntityIds1 = search.Ids,
                    TypeLike = keys.StandInRelation
                });

                search.Ids = realRelations.Where(x => x.value == keys.ActiveIdentifier).Select(x => x.entityId2).ToList();

                if(search.Ids.Count == 0)
                    search.Ids.Add(long.MaxValue); //This should never be found, and should ensure nothing is found in the search
            }

            return search;
        }
    }
}