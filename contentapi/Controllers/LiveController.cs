using System.Net.WebSockets;
using contentapi.Live;
using contentapi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace contentapi.Controllers;

[Authorize()]
public class LiveController : BaseController
{
    public LiveController(BaseControllerServices services) : base(services) 
    { 

    }

    [HttpGet("ws")]
    public Task<ActionResult<string>> WebSocketListenAsync()
    {
        services.logger.LogDebug($"ws METHOD: {HttpContext.Request.Method}, HEADERS: " +
            JsonConvert.SerializeObject(HttpContext.Request.Headers, 
                Formatting.None, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));

        return MatchExceptions(async () =>
        {
            //I have NO idea if returning an action from a websocket request makes any sense, or 
            //if the middleware gets completely wrecked or something!
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                services.logger.LogInformation("ws Websocket starting!");

                using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

                try
                {
                    using var memStream = new MemoryStream();
                    using var reader = new StreamReader(memStream);
                }
                catch(Exception ex)
                {
                    services.logger.LogError("Exception in websocket: " + ex.ToString());
                    await socket.SendObjectAsync(new WebSocketResponse()
                    {
                        type = "unexpected",
                        error = $"Unhandled exception: {ex}"
                    });
                    //Do NOT close with error! We want to reserve websocket errors for network errors and whatever. If 
                    //the SYSTEM encounters an error, we will tell you about it!
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, ex.Message, CancellationToken.None);
                    return ex.Message;
                }

                return "Socket closed on server successfully";
            }
            else
            {
                throw new RequestException("You must send a websocket request to this endpoint!");
            }
        });
    }
}