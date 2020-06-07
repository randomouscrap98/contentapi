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
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class ReadController : BaseSimpleController
    {
        protected ILanguageService docService;
        protected ChainService service;

        protected JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        public ReadController(ILogger<BaseSimpleController> logger, ILanguageService docService, ChainService service)
            : base(logger)
        {
            this.docService = docService;
            this.service = service;
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

        public class RelationListenQuery 
        { 
            public long lastId {get;set;} = -1;
            public Dictionary<string, string> statuses {get;set;} = new Dictionary<string, string>();
            public List<long> clearNotifications {get;set;} = new List<long>();
            public List<string> chains {get;set;}
        }

        public class ListenerQuery
        {
            public Dictionary<string, Dictionary<string, string>> lastListeners {get;set;} = new Dictionary<string, Dictionary<string, string>>();
            public List<string> chains {get;set;}
        }

        public class ListenEndpointResult
        {
            public Dictionary<string, Dictionary<string, string>> listeners {get;set;}
            public Dictionary<string, List<ExpandoObject>> chains {get;set;}
            public long lastId {get;set;}
        }


        [HttpGet("listen")]
        [Authorize]
        public Task<ActionResult<ListenEndpointResult>> ListenAsync([FromQuery]Dictionary<string, List<string>> fields, [FromQuery]string listeners, [FromQuery]string actions, CancellationToken cancelToken)
        {
            return ThrowToAction(async () =>
            {
                var listenerObject = JsonSerializer.Deserialize<ListenerQuery>(listeners ?? "null", jsonOptions);
                var actionObject = JsonSerializer.Deserialize<RelationListenQuery>(actions ?? "null", jsonOptions);

                RelationListenChainConfig rConfig = null;
                ListenerChainConfig lConfig = null;

                if (actionObject != null)
                {
                    rConfig = new RelationListenChainConfig() { 
                        lastId = actionObject.lastId, 
                        chain = actionObject.chains, 
                        clearNotifications = actionObject.clearNotifications 
                    };
                    rConfig.statuses = actionObject.statuses.ToDictionary(x => long.Parse(x.Key), y => y.Value);
                }

                if(listenerObject != null)
                {
                    lConfig = new ListenerChainConfig() { chain = listenerObject.chains };
                    lConfig.lastListeners = listenerObject.lastListeners.ToDictionary(
                        x => long.Parse(x.Key),
                        y => y.Value.ToDictionary(k => long.Parse(k.Key), v => v.Value));
                }

                var result = await service.ListenAsync(fields, lConfig, rConfig, GetRequesterNoFail(), cancelToken);

                return new ListenEndpointResult() 
                { 
                    chains = result.chain,
                    listeners = result.listeners?.ToDictionary(x => x.Key.ToString(), x => x.Value.ToDictionary(k => k.ToString(), v => v.Value)),
                    lastId = result.lastId
                };
            });
        }

        [HttpGet("listen/docs")]
        public Task<ActionResult<string>> ListenDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("doc.read.listen", "en")));
        }
    }
}