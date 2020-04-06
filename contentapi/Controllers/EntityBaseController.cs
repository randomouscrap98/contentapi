using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class EntityWrapper : Entity
    {
        public List<EntityValue> Values = new List<EntityValue>();
        public List<EntityRelation> Relations = new List<EntityRelation>();
    }

    public class EntityWrapperProfile : Profile
    {
        public EntityWrapperProfile()
        {
            CreateMap<EntityWrapper, Entity>().ReverseMap();//.ForMember(x => x.NameLike, o => o.MapFrom(s => s.Username));
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class EntityBaseController : ControllerBase
    {
        protected ILogger<EntityBaseController> logger;
        protected IEntityProvider entityProvider;
        protected IMapper mapper;

        public EntityBaseController(ILogger<EntityBaseController> logger, IEntityProvider provider, IMapper mapper)
        {
            this.logger = logger;
            this.entityProvider = provider;
            this.mapper = mapper;
        }

        //Move this stuff into a service? It all needs to be testable
        protected async Task<Entity> FindSingleByNameAsync(string name)
        {
            var entities = await entityProvider.GetEntitiesAsync(new EntitySearch() { NameLike = name });

            if (entities.Count > 1)
                throw new InvalidOperationException($"Found more than one entity with name {name}");

            return entities.FirstOrDefault(); //This will be null for 0 entities, which is fine.
        }

        protected async Task<Entity> FindSingleByIdAsync(long id)
        {
            var search = new EntitySearch();
            search.Ids.Add(id);
            var entities = await entityProvider.GetEntitiesAsync(search);
            
            if (entities.Count > 1)
                throw new InvalidOperationException($"Found more than one entity with id {id}");

            return entities.FirstOrDefault(); //This will be null for 0 entities, which is fine.
        }

        protected EntityValue QuickValue(string key, string value)
        {
            return new EntityValue()
            {
                createDate = DateTime.Now,
                key = key,
                value = value
            };
        }

        protected EntityWrapper QuickEntity(string name, string content = null)
        {
            return new EntityWrapper()
            {
                createDate = DateTime.Now,
                name = name,
                content = content
            };
        }

        protected async Task<List<EntityWrapper>> SearchAsync(EntitySearch search)
        {
            //var entities = await entityProvider.GetEntitiesAsync(search); //entityProvider.ApplyEntitySearch(entityProvider.GetQueryable<Entity>(), search);
            //var wrappers = entities.Select(x => mapper.Map<EntityWrapper>(x));
            var bigGroup = from e in entityProvider.ApplyEntitySearch(entityProvider.GetQueryable<Entity>(), search)
                           join v in entityProvider.GetQueryable<EntityValue>() on e.id equals v.entityId into evs
                           from v in evs.DefaultIfEmpty()
                           join r in entityProvider.GetQueryable<EntityRelation>() on e.id equals r.entityId2 into evrs
                           from r in evrs.DefaultIfEmpty()
                           select new { Entity = e, Value = v, Relation = r};
            
            return (await entityProvider.GetList(bigGroup)).ToLookup(x => x.Entity.id).Select(x => mapper.Map(x.First().Entity, new EntityWrapper()
            {
                Values = x.Select(x => x.Value).ToList(),
                Relations = x.Select(x => x.Relation).ToList()
            })).ToList();
        }

        protected async Task WriteAsync(EntityWrapper entity)
        {
            //First, write the entity. Then TRY to write everything else. If ANYTHING fails,
            //delete the entity.
            await entityProvider.WriteAsync(entity);

            try
            {
                entity.Values.ForEach(x => x.entityId = entity.id);
                entity.Relations.ForEach(x => x.entityId2 = entity.id); //Assume relations are all parents. a user has perms ON this entity, a category OWNS this entity, etc.
                var allWrite = new List<EntityBase>();
                allWrite.AddRange(entity.Values);
                allWrite.AddRange(entity.Relations);
                await entityProvider.WriteAsync(allWrite.ToArray());
            }
            catch(Exception ex)
            {
                logger.LogError($"Exception while writing entitywrapper: {ex}");
                await entityProvider.DeleteAsync(entity);
            }
        }
    }
}