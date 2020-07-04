using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Constants;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    //public class ReadControllerProfile : Profile
    //{
    //    public ReadControllerProfile()
    //    {
    //        //Input
    //        CreateMap<ReadController.RelationListenQuery, RelationListenChainConfig>();
    //        CreateMap<ReadController.ListenerQuery, ListenerChainConfig>();

    //        //output
    //        CreateMap<ListenResult, ReadController.ListenEndpointResult>();
    //    }
    //}

    public class ReadController : BaseSimpleController
    {
        protected ILanguageService docService;
        protected ChainService service;
        protected RelationListenerService relationListenerService;
        protected IMapper mapper;

        //protected JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        //{
        //    PropertyNameCaseInsensitive = true
        //};

        public ReadController(ILogger<BaseSimpleController> logger, ILanguageService docService, ChainService service, 
            RelationListenerService relationListenerService, IMapper mapper)
            : base(logger)
        {
            this.docService = docService;
            this.service = service;
            this.relationListenerService = relationListenerService;
            this.mapper = mapper;
        }

        protected override Task SetupAsync() { return service.SetupAsync(); }

        [HttpGet("chain")]
        public Task<ActionResult<Dictionary<string, List<ExpandoObject>>>> ChainAsync([FromQuery]List<string> requests, [FromQuery]Dictionary<string, List<string>> fields)
        {
            return ThrowToAction(() => service.ChainAsync(requests, fields, GetRequesterNoFail()));
        }

        [HttpGet("chain/docs")]
        public Task<ActionResult<string>> ChainDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("doc.read.chain", "en")));
        }

        //public class RelationListenQuery 
        //{ 
        //    public long lastId {get;set;} = -1;
        //    public Dictionary<string, string> statuses {get;set;} = new Dictionary<string, string>();
        //    public List<long> clearNotifications {get;set;} = new List<long>();
        //    public List<string> chains {get;set;}
        //}

        //public class ListenerQuery
        //{
        //    public Dictionary<string, Dictionary<string, string>> lastListeners {get;set;} = new Dictionary<string, Dictionary<string, string>>();
        //    public List<string> chains {get;set;}
        //}

        //public class ListenEndpointResult
        //{
        //    public Dictionary<string, Dictionary<string, string>> listeners {get;set;}
        //    public Dictionary<string, List<ExpandoObject>> chains {get;set;}
        //    public List<ModuleMessage> moduleMessages {get;set;}
        //    public long lastId {get;set;}
        //    public long lastModuleId {get;set;}
        //    public List<string> warnings {get;set;} = new List<string>();
        //}

        //protected Dictionary<string, Dictionary<string, string>> ConvertListeners(Dictionary<long, Dictionary<long, string>> listeners)
        //{
        //    return listeners?.ToDictionary(x => x.Key.ToString(), x => x.Value.ToDictionary(k => k.Key.ToString(), v => v.Value));
        //}

        //protected Dictionary<long, Dictionary<long, string>> ConvertListeners(Dictionary<string, Dictionary<string, string>> listeners)
        //{
        //    return listeners?.ToDictionary(x => long.Parse(x.Key), y => y.Value.ToDictionary(k => long.Parse(k.Key), v => v.Value));
        //}


        [HttpGet("listen")]
        [Authorize]
        public Task<ActionResult<ListenResult>> ListenAsync([FromQuery]Dictionary<string, List<string>> fields, 
            [FromQuery]string listeners, [FromQuery]string actions, [FromQuery]string modules, CancellationToken cancelToken)
        {
            //HttpContext.
            return ThrowToAction(async () =>
            {

                //RelationListenChainConfig rConfig = null;
                //ListenerChainConfig lConfig = null;

                //if (actionObject != null)
                //{
                //    rConfig = mapper.Map<RelationListenChainConfig>(actionObject); 
                //    rConfig.statuses = actionObject.statuses.ToDictionary(x => long.Parse(x.Key), y => y.Value);
                //}

                //if(listenerObject != null)
                //{
                //    lConfig = mapper.Map<ListenerChainConfig>(listenerObject); //new ListenerChainConfig() { chain = listenerObject.chains };
                //    lConfig.lastListeners = ConvertListeners(listenerObject.lastListeners);
                //}

            //Newtonsoft.Json.JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(test);
                var lConfig = JsonConvert.DeserializeObject<ListenerChainConfig>(listeners ?? "null"); //, jsonOptions);
                var rConfig = JsonConvert.DeserializeObject<RelationListenChainConfig>(actions ?? "null"); //, jsonOptions);
                var mConfig = JsonConvert.DeserializeObject<ModuleChainConfig>(modules ?? "null"); //, jsonOptions);
                var result = await service.ListenAsync(fields, lConfig, rConfig, mConfig, GetRequesterNoFail(), cancelToken);

                //var returnResult = mapper.Map<ListenEndpointResult>(result);
                //returnResult.listeners = ConvertListeners(result.listeners);
                return result;
            });
        }

        [HttpGet("listen/docs")]
        public Task<ActionResult<string>> ListenDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("doc.read.listen", "en")));
        }

        //Keep this around for debugging purposes
        //[HttpGet("listenersnow")]
        //public Task<ActionResult<Dictionary<string, Dictionary<string, string>>>> InstantListen([FromQuery]List<long> parentIds)
        //{
        //    return ThrowToAction(() => Task.FromResult(ConvertListeners(relationListenerService.GetListenersAsDictionary(relationListenerService.GetInstantListeners(parentIds), parentIds))));
        //}
    }
}