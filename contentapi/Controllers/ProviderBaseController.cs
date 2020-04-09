using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Models;
using contentapi.Services.Extensions;
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

        public async Task<List<EntityWrapper>> SearchExpandAsync(EntitySearch search, bool expand)
        {
            if(expand)
                return await entityProvider.SearchAsync(search);
            else
                return (await entityProvider.GetEntitiesAsync(search)).Select(x => new EntityWrapper(x)).ToList();
        }

        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public async Task<EntityWrapper> FindByNameAsync(string name, bool expand = false)
        {
            return (await SearchExpandAsync(new EntitySearch() {NameLike = name}, expand)).OnlySingle();
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
            return (await SearchExpandAsync(search, expand)).OnlySingle();
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
            return (await entityProvider.GetEntityValuesAsync(valueSearch)).OnlySingle();
        }
    }
}