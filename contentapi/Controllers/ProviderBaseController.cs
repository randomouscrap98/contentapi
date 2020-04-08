using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi
{
    public class ProviderExtensionProfile : Profile 
    {
        public ProviderExtensionProfile()
        {
            CreateMap<EntityWrapper, Entity>().ReverseMap();
            CreateMap<EntitySearch, EntitySearchBase>().ReverseMap();
        }
    }

    /// <summary>
    /// A bunch of methods extending the existing IProvider
    /// </summary>
    /// <remarks>
    /// Even though this extends from controller, it SHOULD NOT EVER use controller functions
    /// or fields or any of that. This is just a little silliness, I'm slapping stuff together.
    /// This is still testable without it being a controller though: please test sometime.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public abstract class ProviderBaseController : ControllerBase
    {
        protected ILogger<ProviderBaseController> logger;
        protected IEntityProvider entityProvider;
        protected IMapper mapper;

        public ProviderBaseController(ILogger<ProviderBaseController> logger, IEntityProvider provider, IMapper mapper)
        {
            this.logger = logger;
            this.entityProvider = provider;
            this.mapper = mapper;
        }

        /// <summary>
        /// Get a single element, return null if none, or fail on multiple (throw exception)
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T OnlySingle<T>(IEnumerable<T> list)
        {
            if(list.Count() > 1)
                throw new InvalidOperationException("Multiple values found; expected 1");
            
            return list.FirstOrDefault();
        }

        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public async Task<EntityWrapper> FindByNameAsync(string name, bool expand = false)
        {
            return OnlySingle(await SearchAsync(new EntitySearch() { NameLike = name}, expand));
        }

        /// <summary>
        /// Find some entity by id 
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public async Task<EntityWrapper> FindByIdAsync(long id, bool expand = false)
        {
            var search = new EntitySearch();
            search.Ids.Add(id);
            return OnlySingle(await SearchAsync(search, expand));
        }

        /// <summary>
        /// Apply various limits to a search
        /// </summary>
        /// <param name="search"></param>
        /// <typeparam name="S"></typeparam>
        /// <returns></returns>
        public S LimitSearch<S>(S search) where S : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }

        /// <summary>
        /// Find a value by key/value/id (added constraints)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<EntityValue> FindValueAsync(string key, string value = null, long id = -1)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = key };
            if(value != null)
                valueSearch.ValueLike = value;
            if(id > 0)
                valueSearch.EntityIds.Add(id);
            return OnlySingle(await entityProvider.GetEntityValuesAsync(valueSearch));
        }

        /// <summary>
        /// Get an easy preformated EntityValue
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public EntityValue QuickValue(string key, string value)
        {
            return new EntityValue()
            {
                createDate = DateTime.Now,
                key = key,
                value = value
            };
        }

        /// <summary>
        /// Get an easy preformated entity
        /// </summary>
        /// <param name="name"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public EntityWrapper QuickEntity(string name, string content = null)
        {
            return new EntityWrapper()
            {
                createDate = DateTime.Now,
                name = name,
                content = content
            };
        }
        
        /// <summary>
        /// Get a value from a wrapper
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetValue(EntityWrapper entity, string key)
        {
            var values = entity.Values.Where(x => x.key == key);

            if(values.Count() != 1)
                throw new InvalidOperationException($"Not a single value for key: {key}");
            
            return values.First().value;
        }

        public bool HasValue(EntityWrapper entity, string key)
        {
            return entity.Values.Any(x => x.key == key);
        }

        public async Task<List<EntityWrapper>> SearchAsync(EntitySearch search, bool expand)
        {
            if(expand)
                return await SearchAndLinkAsync(search);
            else
                return (await entityProvider.GetEntitiesAsync(search)).Select(x => mapper.Map<EntityWrapper>(x)).ToList();
        }

        public async Task<List<EntityWrapper>> SearchAndLinkAsync(EntitySearch search)
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

        public async Task WriteAsync(EntityWrapper entity)
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