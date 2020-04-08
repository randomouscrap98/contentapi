using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Models;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class HistoricEntityProvider //: IHistoricEntityProvider
    {
        public IEntityProvider Provider { get; }

        protected ILogger<HistoricEntityProvider> logger;
        protected IMapper mapper;

        public HistoricEntityProvider(ILogger<HistoricEntityProvider> logger, IEntityProvider provider, IMapper mapper)
        {
            Provider = provider;
            this.logger = logger;
            this.mapper = mapper;
        }

        public async Task<List<EntityWrapper>> LinkEntitiesAsync(IQueryable<Entity> queryable)
        {
            var bigGroup = from e in queryable //entityProvider.ApplyEntitySearch(entityProvider.GetQueryable<Entity>(), search)
                           join v in Provider.GetQueryable<EntityValue>() on e.id equals v.entityId into evs
                           from v in evs.DefaultIfEmpty()
                           join r in Provider.GetQueryable<EntityRelation>() on e.id equals r.entityId2 into evrs
                           from r in evrs.DefaultIfEmpty()
                           select new { Entity = e, Value = v, Relation = r};
            
            return (await Provider.GetList(bigGroup)).ToLookup(x => x.Entity.id).Select(x => mapper.Map(x.First().Entity, new EntityWrapper()
            {
                Values = x.Select(x => x.Value).ToList(),
                Relations = x.Select(x => x.Relation).ToList()
            })).ToList();
        }

        protected void SetEntityAsNew(EntityWrapper entity)
        {
            entity.id = 0;
            entity.Values.ForEach(x => x.id = 0);
            entity.Relations.ForEach(x => x.id = 0); //Assume relations are all parents. a user has perms ON this entity, a category OWNS this entity, etc.
        }

        public async Task WriteNewAsync(EntityWrapper entity)
        {
            SetEntityAsNew(entity);
            await WriteAsync(entity);
        }

        protected async Task WriteAsync(EntityWrapper entity)
        {
            //First, write the entity. Then TRY to write everything else. If ANYTHING fails,
            //delete the entity.
            await Provider.WriteAsync(entity);

            try
            {
                entity.Values.ForEach(x => x.entityId = entity.id);
                entity.Relations.ForEach(x => x.entityId2 = entity.id); //Assume relations are all parents. a user has perms ON this entity, a category OWNS this entity, etc.
                var allWrite = new List<EntityBase>();
                allWrite.AddRange(entity.Values);
                allWrite.AddRange(entity.Relations);
                await Provider.WriteAsync(allWrite.ToArray());
            }
            catch(Exception ex)
            {
                logger.LogError($"Exception while writing entitywrapper: {ex}");
                await Provider.DeleteAsync(entity);
            }
        }
    }
}