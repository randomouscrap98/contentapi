using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Configs;
using contentapi.Services;
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

    public class GCData
    {
        public bool blocking {get;set;}
        public long memoryBefore {get;set;}
        public long memoryAfter {get;set;}
    }


    [Route("api/[controller]")]
    [ApiController]
    public class TestController : BaseSimpleController
    {
        protected IEntityProvider provider;
        protected IPermissionService permissions;
        //protected ICodeTimer timer;

        public TestController(BaseSimpleControllerServices services, IEntityProvider provider, IPermissionService permissions) //, ICodeTimer timer) 
            : base(services) 
        { 
            this.provider = provider;
            this.permissions = permissions;
        }

        public class TestData
        {
            public int EntityCount {get;set;}= -1;
            public int ValueCount {get;set;}= -1;
            public int RelationCount {get;set;}= -1;
        }

        [HttpGet("info")]
        public ActionResult<ExpandoObject> GetInfo()
        {
            dynamic one = new ExpandoObject();
            //one.versions = (new[] { typeof(IEntityProvider), typeof(Startup) });
            one.versions = new Dictionary<string, string>()
            {
                { "entitysystem", $"{typeof(IEntityProvider).Assembly.GetName().Version} " },
                { "contentapi", typeof(Startup).Assembly.GetName().Version.ToString() },
            };
            return (ExpandoObject)one;
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

        [HttpGet("dynamic")]
        public ActionResult<List<ExpandoObject>> GetDynamic()
        {
            dynamic one = new ExpandoObject();
            one.whatever = "yes";
            one.thisisnum = 56;

            dynamic two = new ExpandoObject();
            two.whatever = new List<int>() { 1, 2, 8 };
            two.aagh = new Dictionary<string, string>() { {"a", "432"}};

            return new List<ExpandoObject>()
            {
                one,
                two
            };
        }

        [HttpGet("headers")]
        public ActionResult<Dictionary<string, string>> GetHeaders()
        {
            return this.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString());
        }

        [HttpGet("memory")]
        public ActionResult<long> GetMemory()
        {
            return GC.GetTotalMemory(false);
        }

        [HttpGet("gc")]
        public Task<ActionResult<GCData>> GarbageCollect()
        {
            return ThrowToAction<GCData>(() =>
            {
                if(!permissions.IsSuper(GetRequesterNoFail()))
                    throw new ForbiddenException("Must be super to garbage collect!");

                var result = new GCData() { blocking = true, memoryBefore = GC.GetTotalMemory(false) };

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, true, true);

                result.memoryAfter = GC.GetTotalMemory(false);

                return Task.FromResult(result); //Task.FromResult($"Completed garbage collection! (blocking) Before: {memory/1024/1024}mb, after: {afterMemory/1024/1024}mb");
            });
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

        //[HttpGet("writeperformance")]
        //public ActionResult<string> WritePerformance()
        //{
        //    timer.
        //}
    }
}