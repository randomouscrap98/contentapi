using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace contentapi
{
    public class WebSocketEcho
    {
        protected ILogger<WebSocketEcho> logger;

        public WebSocketEcho(ILogger<WebSocketEcho> logger)
        {
            this.logger = logger;
        }
        
        public async Task Echo(HttpContext context, WebSocket socket)//, CancellationToken token)
        {
            logger.LogTrace("Websocket Echo started");

            var token = CancellationToken.None;

            try
            {
                using(var memStream = new MemoryStream())
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
            }
            catch(WebSocketException ex)
            {
                logger.LogError($"Websocket exception: {ex.Message}");
            }
        }
    }
}