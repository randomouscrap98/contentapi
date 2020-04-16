using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi
{
    public class ControllerServices//<T>
    {
        public IEntityProvider provider;
        public IMapper mapper;
        public Keys keys;
        public SystemConfig systemConfig;

        public ControllerServices(IEntityProvider provider, IMapper mapper, Keys keys, IOptionsMonitor<SystemConfig> systemConfig)
        {
            this.provider = provider;
            this.mapper = mapper;
            this.keys = keys;
            this.systemConfig = systemConfig.CurrentValue;
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
    public abstract class SimpleBaseController : ControllerBase
    {
        protected ControllerServices services;
        protected ILogger<SimpleBaseController> logger;
        
        protected Keys keys => services.keys;

        public SimpleBaseController(ControllerServices services, ILogger<SimpleBaseController> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        protected long GetRequesterUid()
        {
            //Look for the UID from the JWT 
            var id = User.FindFirstValue(services.keys.UserIdentifier);

            if(id == null)
                throw new InvalidOperationException("User not logged in!");
            
            return long.Parse(id);
        }

        protected long GetRequesterUidNoFail()
        {
            try { return GetRequesterUid(); }
            catch { return -1; }
        }

        protected void FailUnlessRequestSuper()
        {
            if(!services.systemConfig.SuperUsers.Contains(GetRequesterUidNoFail()))
                throw new InvalidOperationException("Must be super user to perform this action!");
        }

        protected EntityPackage NewEntity(string name, string content = null)
        {
            return new EntityPackage()
            {
                Entity = new Entity() { name = name, content = content }
            };
        }

        protected EntityValue NewValue(string key, string value)
        {
            return new EntityValue() 
            {
                key = key, 
                value = value, 
                createDate = null 
            };
        }

        protected EntityRelation NewRelation(long parent, string type, string value = null)
        {
            return new EntityRelation()
            {
                entityId1 = parent,
                type = type,
                value = value,
                createDate = null
            };
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
        /// Apply various limits to a search
        /// </summary>
        /// <param name="search"></param>
        /// <typeparam name="S"></typeparam>
        /// <returns></returns>
        protected virtual EntitySearch LimitSearch(EntitySearch search) //where S : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }

    }
}