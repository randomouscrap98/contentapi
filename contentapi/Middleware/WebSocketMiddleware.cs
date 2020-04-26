using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace contentapi.Middleware
{
    public class WebSocketMiddlewareConfig
    {
        public string BaseRoute = "/api/ws/";

        public Dictionary<string, Func<HttpContext, WebSocket, Task>> RouteHandlers = 
            new Dictionary<string, Func<HttpContext, WebSocket, Task>>();
    }

    public class WebSocketMiddleware
    {
        private RequestDelegate next;

        protected WebSocketMiddlewareConfig config;

        public WebSocketMiddleware(RequestDelegate next, WebSocketMiddlewareConfig config)
        {
            this.next = next;
            this.config = config;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var pathString = context.Request.Path.ToString();

            if (pathString.StartsWith(config.BaseRoute))// == "/api/test/wsecho")//.StartsWithSegments("/ws"))
            {
                var isSocketRequest = context.WebSockets.IsWebSocketRequest;

                if (context.WebSockets.IsWebSocketRequest)
                {
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                    var edgeRoute = pathString.Replace(config.BaseRoute, "");

                    if(config.RouteHandlers.ContainsKey(edgeRoute))
                        await config.RouteHandlers[edgeRoute](context, webSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            else
            {
                await next(context);
            }
        }
    }
}