
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ProviderBaseController
    {
        public TestController(ILogger<TestController> logger, IEntityProvider entityProvider, IMapper mapper) 
            : base(logger, entityProvider, mapper)
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

        //[HttpGet("historic")]
        //public async Task<ActionResult> UserHistoric()
        //{
        //    var newWhatever = EntityWrapperExtensions.QuickEntity("someTest")
        //        .AddValue("value1", "key1")
        //        .AddValue("value2", "key2")
        //        .AddRelation(69, "relation1");
        //    
        //    await WriteHistoric(newWhatever);
        //    newWhatever.GetValueRaw("value1").value = "updatedKey (except it's actually value lmao)";
        //    await WriteHistoric(newWhatever);

        //    return Ok($"it at least completed. id: {newWhatever.id}");
        //}
    }
}