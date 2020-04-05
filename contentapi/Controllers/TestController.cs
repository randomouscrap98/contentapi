
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        protected ILogger<TestController> logger;
        protected IEntityProvider entityProvider;

        public TestController(ILogger<TestController> logger, IEntityProvider entityProvider)
        {
            this.logger = logger;
            this.entityProvider = entityProvider;
        }

        public class TestData
        {
            public int EntityCount {get;set;}= -1;
            public int ValueCount {get;set;}= -1;
            public int RelationCount {get;set;}= -1;
        }

        [HttpGet]
        public async Task<ActionResult<TestData>> TestGet()
        {
            var entities = await entityProvider.GetEntitiesAsync(new EntitySearch()); //This should get all?
            var values = await entityProvider.GetEntityValuesAsync(new EntityValueSearch()); //This should get all?
            var relations = await entityProvider.GetEntityRelationsAsync(new EntityRelationSearch()); //This should get all?

            return new TestData()
            {
                EntityCount = entities.Count,
                ValueCount = values.Count,
                RelationCount = relations.Count
            };
        }
    }
}