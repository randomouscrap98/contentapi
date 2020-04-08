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

    public class EntityControllerProfile : Profile
    {
        public EntityControllerProfile()
        {
            CreateMap<EntityWrapper, Entity>().ReverseMap();//.ForMember(x => x.NameLike, o => o.MapFrom(s => s.Username));
            CreateMap<EntitySearch, EntitySearchBase>().ReverseMap();
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

        protected T OnlySingle<T>(IEnumerable<T> list)
        {
            if(list.Count() > 1)
                throw new InvalidOperationException("Multiple values found; expected 1");
            
            return list.FirstOrDefault();
        }

        //Move this stuff into a service? It all needs to be testable
        protected async Task<E> FindByNameAsync<E>(string name) where E : Entity
        {
            return OnlySingle(await SearchAsync<E>(new EntitySearch() { NameLike = name}));
        }

        protected async Task<E> FindByIdAsync<E>(long id) where E : Entity
        {
            var search = new EntitySearch();
            search.Ids.Add(id);
            return OnlySingle(await SearchAsync<E>(search));

            //if(typeof(E) != typeof(EntityWrapper))
            //    //return OnlySingle(await entityProvider.GetList(entityProvider.ApplyGeneric(entityProvider.GetQueryable<E>(), search)));
            //else
            //    return (E)OnlySingle(await LinkedSearchAsync(search));
        }

        //protected async Task<EntityWrapper> FindByIdAsync(long id)
        //{
        //    var search = new EntitySearch();
        //    search.Ids.Add(id);
        //    return OnlySingle(await (search));
        //}

        protected E LimitSearch<E>(E search) where E : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }

        protected async Task<EntityValue> FindValueAsync(string key, string value)
        {
            return OnlySingle(await entityProvider.GetEntityValuesAsync(new EntityValueSearch() { KeyLike = key, ValueLike = value}));
        }

        protected async Task<EntityValue> FindValueAsync(string key, long id)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = key };
            valueSearch.EntityIds.Add(id);
            return OnlySingle(await entityProvider.GetEntityValuesAsync(valueSearch));
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
        
        protected string GetValue(EntityWrapper entity, string key)
        {
            var values = entity.Values.Where(x => x.key == key);

            if(values.Count() != 1)
                throw new InvalidOperationException($"Not a single value for key: {key}");
            
            return values.First().value;
        }

        protected bool HasValue(EntityWrapper entity, string key)
        {
            return entity.Values.Any(x => x.key == key);
        }

        protected async Task<List<E>> SearchAsync<E>(EntitySearch search) where E : Entity
        {
            if(typeof(E) == typeof(Entity))
                return (await entityProvider.GetEntitiesAsync(search)).Cast<E>().ToList();
            else
                return (await SearchAndLinkAsync(search)).Cast<E>().ToList();
        }

        protected async Task<List<EntityWrapper>> SearchAndLinkAsync(EntitySearch search)
        {
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