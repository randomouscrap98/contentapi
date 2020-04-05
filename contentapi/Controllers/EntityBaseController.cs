using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EntityBaseController : ControllerBase
    {
        protected ILogger<EntityBaseController> logger;
        protected IEntityProvider entityProvider;

        public EntityBaseController(ILogger<EntityBaseController> logger, IEntityProvider provider)
        {
            this.logger = logger;
            this.entityProvider = provider;
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

        protected async Task<List<EntityPackage>> SearchAndExpand(EntitySearch search)
        {
            return await entityProvider.ExpandAsync((await entityProvider.GetEntitiesAsync(search)).ToArray());
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

        protected EntityPackage QuickPackage(string name, string content = null)
        {
            return new EntityPackage()
            {
                Entity = new Entity()
                {
                    createDate = DateTime.Now,
                    name = name,
                    content = content
                }
            };
        }
    }
}