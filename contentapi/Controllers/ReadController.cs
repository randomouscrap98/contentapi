using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace contentapi.Controllers
{
    public class ReadController : BaseSimpleController
    {
        protected ILanguageService docService;
        protected ChainService service;
        protected ChainServiceConfig serviceConfig;
        protected RelationListenerService relationListenerService;
        protected IMapper mapper;

        public ReadController(ILogger<BaseSimpleController> logger, ILanguageService docService, ChainService service, 
            RelationListenerService relationListenerService, IMapper mapper, ChainServiceConfig serviceConfig)
            : base(logger)
        {
            this.docService = docService;
            this.service = service;
            this.relationListenerService = relationListenerService;
            this.mapper = mapper;
            this.serviceConfig = serviceConfig;
        }

        protected override Task SetupAsync() { return service.SetupAsync(); }


        [HttpGet("chain")]
        public Task<ActionResult<Dictionary<string, List<ExpandoObject>>>> ChainAsync([FromQuery]List<string> requests, [FromQuery]Dictionary<string, List<string>> fields)
        {
            return ThrowToAction(() => service.ChainAsync(requests, fields, GetRequesterNoFail()));
        }

        [HttpGet("chain/info")]
        public Task<ActionResult<ExpandoObject>> ChainInfoAsync()
        {
            return ThrowToAction(() =>
            {
                dynamic result = new ExpandoObject();
                result.endpoints = typeof(ChainServices).GetProperties().Select(x => x.Name);
                result.requestregex = serviceConfig.RequestRegex;
                result.chainregex = serviceConfig.ChainRegex;
                result.maxchains = serviceConfig.MaxChains;
                return Task.FromResult((ExpandoObject)result);
            });
        }

        [HttpGet("chain/docs")]
        public Task<ActionResult<string>> ChainDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("doc.read.chain", "en")));
        }

        [HttpGet("listen")]
        [Authorize]
        public Task<ActionResult<ListenResult>> ListenAsync([FromQuery]Dictionary<string, List<string>> fields, 
            [FromQuery]string listeners, [FromQuery]string actions, [FromQuery]string modules, CancellationToken cancelToken)
        {
            //HttpContext.
            return ThrowToAction(async () =>
            {
                var lConfig = JsonConvert.DeserializeObject<ListenerChainConfig>(listeners ?? "null"); //, jsonOptions);
                var rConfig = JsonConvert.DeserializeObject<RelationListenChainConfig>(actions ?? "null"); //, jsonOptions);
                //var mConfig = JsonConvert.DeserializeObject<ModuleChainConfig>(modules ?? "null"); //, jsonOptions);
                var result = await service.ListenAsync(fields, lConfig, rConfig, /*mConfig,*/ GetRequesterNoFail(), cancelToken);

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