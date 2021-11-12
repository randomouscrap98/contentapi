using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Extensions;
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
        protected ITempTokenService<long> tokenService;
        protected Action<Newtonsoft.Json.JsonSerializerSettings> jsonOptionAction;

        public ReadController(BaseSimpleControllerServices services, ILanguageService docService, ChainService service, 
            RelationListenerService relationListenerService, IMapper mapper, ChainServiceConfig serviceConfig,
            ITempTokenService<long> tokenService, Action<Newtonsoft.Json.JsonSerializerSettings> jsonOptionAction)
            : base(services)
        {
            this.docService = docService;
            this.service = service;
            this.relationListenerService = relationListenerService;
            this.mapper = mapper;
            this.serviceConfig = serviceConfig;
            this.tokenService = tokenService;
            this.jsonOptionAction = jsonOptionAction;
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
        public Task<ActionResult<ChainListenResult>> ListenAsync([FromQuery]Dictionary<string, List<string>> fields, 
            [FromQuery]string listeners, [FromQuery]string actions, CancellationToken cancelToken)
        {
            //HttpContext.
            return ThrowToAction(async () =>
            {
                var lConfig = JsonConvert.DeserializeObject<ListenerChainConfig>(listeners ?? "null"); 
                var rConfig = JsonConvert.DeserializeObject<RelationListenChainConfig>(actions ?? "null"); 
                var result = await service.ListenAsync(fields, lConfig, rConfig, /*mConfig,*/ GetRequesterNoFail(), cancelToken);

                return result;
            });
        }

        /// <summary>
        /// The configuration for the listen endpoints. Useful for websocket
        /// </summary>
        public class ListenRequest
        {
            public string auth {get;set;}
            public Dictionary<string, List<string>> fields {get;set;}
            public ListenerChainConfig listeners {get;set;}
            public RelationListenChainConfig actions {get;set;}
        }

        [HttpGet("wsauth")]
        [Authorize]
        public ActionResult<string> GetAuth()
        {
            return tokenService.GetToken(GetRequesterUid());
        }

        [HttpGet("wslisten")]
        public async Task<ActionResult<string>> WebSocketListenAsync()
        {
            logger.LogDebug("wslisten METHOD: " + HttpContext.Request.Method + ", HEADERS: " +
                JsonConvert.SerializeObject(HttpContext.Request.Headers, 
                    Formatting.None, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
            
            //I have NO idea if returning an action from a websocket request makes any sense, or 
            //if the middleware gets completely wrecked or something!
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                logger.LogInformation("wslisten Websocket starting!");

                using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

                try
                {
                    using var memStream = new MemoryStream();
                    using var reader = new StreamReader(memStream);

                    //Since we need a legit result anyway, and we can't do anything until we're configured
                    //through the websocket, just await the full first result.
                    WebSocketReceiveResult result = await socket.ReceiveAsync(memStream, CancellationToken.None);

                    //Loop until the websocket is closed. The outer loop loops on legit recieves only
                    while (!result.CloseStatus.HasValue)
                    {
                        //This is the cancel source for the listener task, and any other things that get 
                        //pre-empted by the websocket read
                        var tokenSource = new CancellationTokenSource();
                        var token = tokenSource.Token;
                        memStream.Position = 0;
                        var sendMessage = await reader.ReadToEndAsync();

                        //At this point, we ALWAYS have a result!! The outer loop loops on legit receives!
                        //If the user sends crap to us, we just fail immediately. Whatever
                        var lrequest = JsonConvert.DeserializeObject<ListenRequest>(sendMessage);

                        //ALWAYS check the token. Again and again.
                        var uid = tokenService.ValidateToken(lrequest.auth);
                        var requester = new Requester() { userId = uid };
                        logger.LogDebug($"Received {result.Count} bytes in websocket listener for uid {uid}");

                        //Tell the user we successfully processed their thing
                        await socket.SendAsync(
                            System.Text.Encoding.UTF8.GetBytes("accepted:" + sendMessage), 
                            WebSocketMessageType.Text, true, CancellationToken.None);

                        //Now start the next read request! But don't await it, we need to do our listen service work!
                        memStream.SetLength(0);
                        var wsTask = socket.ReceiveAsync(memStream, CancellationToken.None);
                        Task<ChainListenResult> listenTask = null;
                        Task completedTask = null;

                        //This inner loop keeps performing listens based on the current listen request object.
                        //As soon as the completed task is a websocket read, we stop doing our regular listen loop
                        while(completedTask != wsTask)
                        {
                            if(listenTask == null)
                            {
                                listenTask = service.ListenAsync(lrequest.fields, lrequest.listeners, 
                                    lrequest.actions, requester, token);
                            }

                            //Now wait for any. We at LEAST know the listener will finish in a 
                            //certain amount of time, so don't worry too much about it.
                            completedTask = await Task.WhenAny(listenTask, wsTask);

                            //If the listener completed, that means we have data to send out!
                            if(completedTask == listenTask)
                            {
                                //Sometimes, the task completed by timing out, which is technically a cancellation?
                                try
                                { 
                                    //Get the data from the completed task
                                    var listenResult = await listenTask;
                                    //Send the data out as a simple json string 
                                    await socket.SendAsync(
                                        System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(listenResult)), 
                                        WebSocketMessageType.Text, true, token);
                                    //Update the original request so it continues to work
                                    lrequest.actions.lastId = listenResult.lastId;
                                    if(listenResult.listeners != null) 
                                        lrequest.listeners.lastListeners = listenResult.listeners;
                                }
                                //If it's a cancellation, just ignore this update and continue, there's nothing for us to use.
                                catch(TaskCanceledException) {}
                                catch(OperationCanceledException) {}
                                //Reset the task so we can restart it
                                listenTask = null;
                            }
                        }

                        //We can be certain that the websocket read task has data, but await it anyway 
                        //just because whatever
                        tokenSource.Cancel(); //Cancel other stuff we're doing, user reads are more important
                        try { await listenTask; }
                        catch(TaskCanceledException) {}
                        tokenSource.Dispose();
                        result = await wsTask;
                    }

                    await socket.CloseAsync(result, CancellationToken.None);
                    return "Complete";
                }
                catch(Exception ex)
                {
                    logger.LogError("Exception in websocket: " + ex.ToString());
                    await socket.SendAsync(
                        System.Text.Encoding.UTF8.GetBytes("error:"+ex.ToString()), 
                        WebSocketMessageType.Text, true, CancellationToken.None);
                    //Do NOT close with error! We want to reserve websocket errors for network errors and whatever. If 
                    //the SYSTEM encounters an error, we will tell you about it!
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, ex.Message, CancellationToken.None);
                    //await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, ex.Message, CancellationToken.None);
                    return ex.Message;
                }
            }
            else
            {
                logger.LogWarning("wslisten not recognized as a websocket connection!");
                return BadRequest("You must send a websocket connection to this endpoint!");
            }
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