using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
            public TimeSpan ListenTimeout {get;set;}
            public TimeSpan ListenGracePeriod {get;set;}
        }

        [HttpGet("system")]
        public ActionResult<SystemData> GetSystem()
        {
            return services.mapper.Map<SystemData>(services.systemConfig); 
        }

        [HttpGet("exception")]
        public ActionResult GetException()
        {
            throw new InvalidOperationException("This is the exception message");
        }

        [HttpGet("wsecho")]
        [Authorize]
        public async Task GetWebsocket(CancellationToken token)
        {
            var context = ControllerContext.HttpContext;
            var isSocketRequest = context.WebSockets.IsWebSocketRequest;

            if (isSocketRequest)
            {
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await Echo(context, webSocket, token);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        }

        protected async Task Echo(HttpContext context, WebSocket socket, CancellationToken token)
        {
            logger.LogTrace("Websocket Echo started");
            try
            {
                var buffer = new byte[4096];
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                while (!result.CloseStatus.HasValue)
                {
                    logger.LogDebug($"Echoing {result.Count} bytes");
                    await socket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, token);
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                }
                
                await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
            catch(WebSocketException ex)
            {
                logger.LogError($"Websocket exception: {ex.Message}");
            }
        }
    }
}