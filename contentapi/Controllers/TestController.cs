using System;
using System.Collections.Generic;
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
    public class TestController : BaseSimpleController
    {
        public TestController(ILogger<TestController> logger, ControllerServices services)
            :base(services, logger) { }

        public class TestData
        {
            public int EntityCount {get;set;}= -1;
            public int ValueCount {get;set;}= -1;
            public int RelationCount {get;set;}= -1;
        }

        [HttpGet]
        public async Task<ActionResult<TestData>> TestGet()
        {
            var entities = await services.provider.GetEntitiesAsync(new EntitySearch()); //This should get all?
            var values = await services.provider.GetEntityValuesAsync(new EntityValueSearch()); //This should get all?
            var relations = await services.provider.GetEntityRelationsAsync(new EntityRelationSearch()); //This should get all?

            return new TestData()
            {
                EntityCount = entities.Count,
                ValueCount = values.Count,
                RelationCount = relations.Count
            };
        }
        
        public class SystemData
        {
            public List<long> SuperUsers {get;set;}
        }

        [HttpGet("system")]
        public ActionResult<SystemData> GetSystem()
        {
            //var config = services.systemConfig;
            return new SystemData()
            {
                SuperUsers = services.systemConfig.SuperUsers
            };
        }

        [HttpGet("exception")]
        public ActionResult GetException()
        {
            throw new InvalidOperationException("This is the exception message");
        }
    }
}