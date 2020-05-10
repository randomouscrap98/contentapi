using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    //NOTE: Many parts of using this service... require intimate knowledge of how the PARTICULAR implementation
    //actually works. Either move away from this or accept it and test OTHER services, because this one is 
    //VERY DIFFICULT to test. Perhaps what will be required from this will become more apparent as you
    //build up the other services.
    public class HistoryService : IHistoryService
    {
        protected ILogger logger;
        protected IEntityProvider provider;
        protected IActivityService activityService;

        public HistoryService(ILogger<HistoryService> logger, IEntityProvider provider, IActivityService activityService)
        {
            this.logger = logger;
            this.provider = provider;
            this.activityService = activityService;
        }

        public void MakeHistoric(Entity entity)
        {
            entity.type = Keys.HistoryKey + (entity.type ?? "");
        }

        /// <summary>
        /// Put a copy of the given entity into history
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task<Entity> CreateHistoricCopyAsync(Entity entity)
        {
            var newEntity = new Entity(entity);
            newEntity.id = 0;
            MakeHistoric(newEntity);
            await provider.WriteAsync(newEntity);
            return newEntity;
        }

        /// <summary>
        /// Create the relation object tying the original to the historic
        /// </summary>
        /// <param name="originalEntity"></param>
        /// <param name="historicCopy"></param>
        /// <returns></returns>
        public EntityRelation NewHistoryLink(Entity originalEntity, Entity historicCopy)
        {
            return new EntityRelation()
            {
                type = Keys.HistoryRelation,
                entityId1 = originalEntity.id,
                entityId2 = historicCopy.id,
                createDate = DateTime.Now
            };
        }

        public Task<List<long>> GetRevisionIdsAsync(long packageId)
        {
            return provider.GetListAsync(
                from r in provider.GetQueryable<EntityRelation>()
                where r.entityId1 == packageId && r.type == Keys.HistoryRelation
                select r.entityId2);
        }

        /// <summary>
        /// Update the given existing entity with the new entity, preserving the history for the original.
        /// </summary>
        /// <param name="updateData"></param>
        /// <param name="originalData"></param>
        /// <returns></returns>
        public async Task UpdateWithHistoryAsync(EntityPackage updateData, long user, EntityPackage originalData = null)
        {
            logger.LogTrace($"WriteHistoric called for entity {updateData.Entity.id}");

            //The original isn't necessary; we can find it using the id from our apparently updated data 
            if(originalData == null)
                originalData = await provider.FindByIdAsync(updateData.Entity.id);

            var history = await CreateHistoricCopyAsync(originalData.Entity);

            try
            {
                //Bring all the existing over to this historic entity
                history.Relink(originalData.Values, originalData.Relations);

                //WE have to link the new stuff to US because we want to write everything all at once 
                originalData.Entity.Relink(updateData.Values, updateData.Relations);

                //Add the historic link back to the history copy from the 
                originalData.Relations.Add(NewHistoryLink(updateData.Entity, history));

                //A special thing: the values and relations need to be NEW for the update data
                updateData.Relations.ForEach(x => x.id = 0);
                updateData.Values.ForEach(x => x.id = 0);

                //We're writing the entirety of the "update" data.
                var writes = updateData.FlattenPackage();

                //Also writing the relinked original stuff.
                writes.AddRange(originalData.Values);
                writes.AddRange(originalData.Relations);

                writes.Add(activityService.MakeActivity(updateData.Entity, user, Keys.UpdateAction, history.id.ToString()));

                await provider.WriteAsync(writes.ToArray());
            }
            catch
            {
                logger.LogError("Failure during historic update, trying to undo... Exception bubbling...");
                await provider.DeleteAsync(history);
                throw;
            }
        }

        public async Task InsertWithHistoryAsync(EntityPackage newData, long user, Action<EntityPackage> modifyBeforeCreate = null)
        {
            if(newData.Entity.id > 0)
                throw new InvalidOperationException("'New' package has non-zero id!");

            var mainEntity = newData.Entity;
            await provider.WriteAsync(mainEntity);

            try
            {
                newData.Relink();
                modifyBeforeCreate?.Invoke(newData);

                var writes = new List<EntityBase>();

               //Must write everything else at the same time. We only wrote the first thing to get the ID
                writes.AddRange(newData.Values);
                writes.AddRange(newData.Relations);

                writes.Add(activityService.MakeActivity(newData.Entity, user, Keys.CreateAction));

                await provider.WriteAsync(writes.ToArray());
            }
            catch
            {
                logger.LogError("Failure during historic insert, trying to undo... Exception bubbling");
                await provider.DeleteAsync(mainEntity);
                throw;
            }
        }

        /// <summary>
        /// Allow "fake" deletion of ANY historic entity (of any type)
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public async Task DeleteWithHistoryAsync(EntityPackage package, long user)
        {
            var historicPart = package.Entity;

            MakeHistoric(historicPart);

            //Notice it is a WRITe and not a delete. The activity extra will include the title.
            await provider.WriteAsync<EntityBase>(
                historicPart, 
                activityService.MakeActivity(historicPart, user, Keys.DeleteAction, package.Entity.name));     
        }

        public EntityPackage ConvertHistoryToUpdate(EntityPackage history)
        {
            var result = new EntityPackage(history);

            //Pull out (literally) the history relation
            var historyLink = result.Relations.Where(x => x.type == Keys.HistoryRelation).OnlySingle();
            result.Relations.RemoveAll(x => x.type == Keys.HistoryRelation);

            //Update the id from history, relink to us (I don't know if relinking matters...)
            result.Entity.id = historyLink.entityId1;
            result.Relink();

            //Finally, mark the type as would normally be
            result.Entity.type = result.Entity.type.Substring(Keys.HistoryKey.Length);

            return result;
        }
    }
}