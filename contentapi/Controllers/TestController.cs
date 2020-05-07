using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class TestControllerProfile : Profile
    {
        public TestControllerProfile()
        {
            CreateMap<TestController.SystemData, SystemConfig>().ReverseMap();
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class TestController : BaseSimpleController
    {
        protected IEntityProvider provider;

        public TestController(Keys keys, ILogger<BaseSimpleController> logger, IEntityProvider provider) 
            : base(keys, logger) 
        { 
            this.provider = provider;
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
            var entities = await provider.GetEntitiesAsync(new EntitySearch()); //This should get all?
            var values = await provider.GetEntityValuesAsync(new EntityValueSearch()); //This should get all?
            var relations = await provider.GetEntityRelationsAsync(new EntityRelationSearch()); //This should get all?

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
            public TimeSpan ListenTimeout {get;set;}
            public TimeSpan ListenGracePeriod {get;set;}
        }

        //[HttpGet("system")]
        //public ActionResult<SystemData> GetSystem()
        //{
        //    return mapper.Map<SystemData>(services.systemConfig); 
        //}

        [HttpGet("exception")]
        public ActionResult GetException()
        {
            throw new InvalidOperationException("This is the exception message");
        }
    }
}