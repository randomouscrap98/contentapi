using System.Net.WebSockets;
using System.Threading.Tasks.Dataflow;
using contentapi.Live;
using contentapi.Search;
using contentapi.Utilities;
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

    protected long ValidateToken(string token)
    {
        try
        {
            var claims = services.authService.ValidateToken(token) ?? throw new TokenException("Couldn't validate token!");
            var userId = services.authService.GetUserId(claims.Claims) ?? throw new TokenException("No valid userID assigned to token! Is it expired?");
            return userId;
        }
        catch(TokenException)
        {
            throw;
        }
        catch(Exception ex)
        {
            throw new TokenException("Error during token validation: " + ex.Message);
        }
    }

    protected async Task<string> WaitForToken(CancellationToken cancelToken, WebSocket socket)
    {
        var receiveItem = await socket.ReceiveObjectAsync<WebSocketRequest>(null, cancelToken);

        if(receiveItem.type != "token")
        {
            throw new RequestException("In current version, you can't perform any actions until you send your user token in a 'token' type request!");
            //response.error = "In current version, you can't perform any actions until you send your user token in a 'token' type request!";
            //sendQueue.Post(response);
            //return null;
        }
        else
        {
            var token = (string)(receiveItem.data ?? throw new RequestException("You must set the 'data' field to your token string!"));
            var userId = ValidateToken(token);

            var response = new WebSocketResponse()
            {
                id = receiveItem.id,
                type = receiveItem.type,
                requestUserId = userId,
                data = $"accepted: {userId}"
            };

            return token; //Tuple.Create(token, userId);
        }
    }

    protected async Task ReceiveLoop(CancellationToken cancelToken, WebSocket socket, BufferBlock<object> sendQueue, string token)
    {
        using var memStream = new MemoryStream();

        while(!cancelToken.IsCancellationRequested)
        {
            //NOTE: the ReceiveObjectAsync throws an exception on close
            var receiveItem = await socket.ReceiveObjectAsync<WebSocketRequest>(memStream, cancelToken);
            var userId = ValidateToken(token); // Validate every time
            var response = new WebSocketResponse()
            {
                id = receiveItem.id,
                type = receiveItem.type,
                requestUserId = userId
            };

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
                    var searchResult = await services.searcher.Search(searchRequest, userId);
                    response.data = searchResult;
                }
                catch(Exception ex)
                {
                    response.error = $"Error during search: {ex.Message}";
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
    public Task<ActionResult<string>> WebSocketListenAsync([FromQuery]long? lastId = null) //long lastId)
    {
        services.logger.LogDebug($"ws METHOD: {HttpContext.Request.Method}, HEADERS: " +
            JsonConvert.SerializeObject(HttpContext.Request.Headers, 
                Formatting.None, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
        
        var realLastId = lastId ?? -1;

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

                //Now, just receive a normal string, it should fit in any normal buffer

                try
                {
                    var tokenResult = await WaitForToken(cancelSource.Token, socket);

                    //Can send and receive at the same time, but CAN'T send/receive multiple at the same time.
                    var receiveTask = ReceiveLoop(cancelSource.Token, socket, sendQueue, tokenResult);
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
                            type = ex is TokenException ? "badtoken" : "unexpected",
                            error = ex is TokenException ? ex.Message : $"Unhandled exception: {ex}"
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