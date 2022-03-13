using System.Net.WebSockets;
using System.Threading.Tasks.Dataflow;
using contentapi.Live;
using contentapi.Search;
using contentapi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace contentapi.Controllers;

public class LiveController : BaseController
{
    public LiveController(BaseControllerServices services) : base(services) 
    { 
    }

    //public class ConfigureLive
    //{
    //    public long lastId {get;set;} = 0;
    //    public string token {get;set;} = "";
    //}

    protected async Task ReceiveLoop(CancellationToken token, WebSocket socket, BufferBlock<object> sendQueue)
    {
        using var memStream = new MemoryStream();

        while(!token.IsCancellationRequested)
        {
            //NOTE: the ReceiveObjectAsync throws an exception on close
            var receiveItem = await socket.ReceiveObjectAsync<WebSocketRequest>(memStream, token);
            var response = new WebSocketResponse()
            {
                id = receiveItem.id,
                type = receiveItem.type
            };

            //Go lookup the user that we think they are
            try
            {
                if (!string.IsNullOrWhiteSpace(receiveItem.token))
                {
                    var claims = services.authService.ValidateToken(receiveItem.token);
                    var userId = services.authService.GetUserId(claims.Claims);
                    response.requestUserId = userId ?? 0;
                }
            }
            catch(Exception ex)
            {
                response.error = $"Token error: {ex.Message}";
                sendQueue.Post(response);
                continue;
            }

            if(receiveItem.type == "ping")
            {
                response.data = new {
                    serverTime = DateTime.UtcNow
                };
            }
            else if(receiveItem.type == "request")
            {
                try
                {
                    var searchRequest = services.mapper.Map<SearchRequests>(receiveItem.data);
                    var searchResult = await services.searcher.Search(searchRequest, response.requestUserId);
                    response.data = searchResult;
                }
                catch(Exception ex)
                {
                    response.error = $"Error during search: {ex.Message}";
                }
            }
            //NOTES: it's far more complicated to CHANGE the live updates during runtime than to just... mmmm.
            //But we want people to be able to update their user token without restarting the websocket? If so,
            //then yes they WILL need to. But then they'll need to handle the error anyway, especially the token
            //error in particular separately from the other errors
            else if(receiveItem.type == "startlive")
            {
                try
                {
                    var lastId = (long)(receiveItem.data ?? -1);

                    //For some reason, the system needs the user view rather than just the id.
                    //ALSO, we need to check the listenUser in the 
                    //var listenUser = 
                }
                catch(Exception ex)
                {
                    response.error = $"Error during live start: {ex.Message}";
                }
            }
            else
            {
                response.error = $"Unknown request type {receiveItem.type}";
            }

            sendQueue.Post(response);
        }
    }

    protected async Task SendLoop(CancellationToken token, WebSocket socket, BufferBlock<object> sendQueue)
    {
        while(!token.IsCancellationRequested)
        {
            var sendItem = await sendQueue.ReceiveAsync(token);
            await socket.SendObjectAsync(sendItem, WebSocketMessageType.Text, token);
            //The only exception this will throw is if they disconnect or something. On disconnect, we'll
            //exit the loop and 
        }
    }

    [HttpGet("ws")] ///{lastId}")]
    public Task<ActionResult<string>> WebSocketListenAsync() //long lastId)
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
                using var cancelSource = new CancellationTokenSource();
                var sendQueue = new BufferBlock<object>();

                try
                {
                    //Can send and receive at the same time, but CAN'T send/receive multiple at the same time.
                    var receiveTask = ReceiveLoop(cancelSource.Token, socket, sendQueue);
                    var listenTask = Task.Delay(int.MaxValue, cancelSource.Token); //Don't even ask! Just spawn the listener!
                    var sendTask = SendLoop(cancelSource.Token, socket, sendQueue);

                    try
                    {
                        await Task.WhenAny(receiveTask, sendTask, listenTask);
                    }
                    finally
                    {
                        //Clean up the tasks
                        cancelSource.Cancel();
                        await Task.WhenAll(receiveTask, sendTask, listenTask);
                    }
                }
                catch(Exception ex)
                {
                    services.logger.LogError("Exception in websocket: " + ex.ToString());

                    //Yes, the websocket COULD close after this check, but it still saves us a LOT of hassle to skip the
                    //common cases of "the websocket is actually closed from the exception"
                    if(socket.State == WebSocketState.Open)
                    {
                        await socket.SendObjectAsync(new WebSocketResponse()
                        {
                            type = "unexpected",
                            error = $"Unhandled exception: {ex}"
                        });

                        //Do NOT close with error! We want to reserve websocket errors for network errors and whatever. If 
                        //the SYSTEM encounters an error, we will tell you about it!
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, ex.Message, CancellationToken.None);
                    }

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