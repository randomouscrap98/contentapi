using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class ListenRequest
    {
        public string userToken {get;set;} = null;
        public List<string> types {get;set;} = null;
    }

    public class WebSocketController : BaseSimpleController
    {
        protected IPermissionService permissionService;

        public WebSocketController(BaseSimpleControllerServices services, IPermissionService pService) : base(services)
        {
            this.permissionService = pService;
        }

        //What happens if a user's token expires, either prematurely or otherwise? Probably need
        //to check their token once in a while!
        [HttpGet("main")]
        [Authorize] 
        public async Task Main([FromQuery]ActivitySearch search)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                logger.LogTrace("Websocket Main started");

                using WebSocket socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                using MemoryStream memStream = new MemoryStream();
                var token = CancellationToken.None;
                //Randomous.EntitySystem.
            }
            else
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }

        [HttpGet("echo")]
        public async Task Echo([FromQuery]ActivitySearch search)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                logger.LogTrace("Websocket Echo started");

                using WebSocket socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                using MemoryStream memStream = new MemoryStream();
                var token = CancellationToken.None;

                try
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(memStream, token);

                    while (!result.CloseStatus.HasValue)
                    {
                        logger.LogDebug($"Echoing {result.Count} bytes");
                        await socket.SendAsync(memStream.ToArray(), result.MessageType, result.EndOfMessage, token);
                        memStream.SetLength(0);
                        result = await socket.ReceiveAsync(memStream, token);
                    }

                    await socket.CloseAsync(result, CancellationToken.None);
                }
                catch (Exception ex) //(WebSocketException ex)
                {
                    logger.LogError($"Websocket exception: {ex.Message}");
                    throw;
                }
            }
            else
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }
    }
}