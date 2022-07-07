using System.Net.WebSockets;
using System.Threading.Tasks.Dataflow;
using contentapi.Live;
using contentapi.Utilities;
using contentapi.data.Views;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using contentapi.data;

namespace contentapi.Controllers;

public class LiveControllerConfig 
{
    public string AnonymousToken {get;set;} = ""; //With an empty string, no anonymous token is set
}


public class LiveController : BaseController
{
    protected ILiveEventQueue eventQueue;
    protected IUserStatusTracker userStatuses;
    protected IPermissionService permissionService;
    protected IHostApplicationLifetime appLifetime;
    protected LiveControllerConfig config;
    private static int nextId = 0;
    protected int trackerId = Interlocked.Increment(ref nextId);

    protected static ConcurrentDictionary<int, WebsocketListenerData> currentListeners = new ConcurrentDictionary<int, WebsocketListenerData>();

    protected class WebsocketListenerData
    {
        public long userId;
        public BufferBlock<object> sendQueue = new BufferBlock<object>();
    }

    protected class WebsocketWriteData
    {
        public string type {get;set;} = "";
        public JObject? @object {get;set;} = null;
        public string? activityMessage {get;set;} = null;
    }

    public LiveController(BaseControllerServices services, ILiveEventQueue eventQueue, IUserStatusTracker userStatuses,
        IPermissionService permissionService, IHostApplicationLifetime appLifetime, LiveControllerConfig config) : base(services) 
    { 
        this.eventQueue = eventQueue;
        this.userStatuses = userStatuses;
        this.permissionService = permissionService;
        this.appLifetime = appLifetime;
        this.config = config;
    }

    protected long ValidateToken(string token)
    {
        try
        {
            //Simple validation, we got the anonymous token
            if(!string.IsNullOrWhiteSpace(config.AnonymousToken) && token == config.AnonymousToken)
                return 0;
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

    protected Task<UserlistResult> GetUserStatusesAsync(long uid, params long[] contentIds)
    {
        return userStatuses.GetUserStatusesAsync(services.searcher, uid, eventQueue.GetAutoContentRequest().fields, "*", contentIds);
    }

    protected async Task AddUserStatusAsync(long userId, long contentId, string status)
    {
        if(userId <= 0)
            throw new InvalidOperationException($"Cannot set user status for user ID {userId}");
        var addAbout = await userStatuses.AddStatusAsync(userId, contentId, status, trackerId);

        //Alert the userlist if it's specifically not such that nothing was removed and nothing was added.
        //If nothing was removed AND nothing was added, that's one of the few instances where we can be sure nothing changed
        if(!(addAbout.Item1 == 0 && !addAbout.Item2))
            await AlertUserlistUpdate(contentId);
    }

    protected async Task RemoveStatusesByTrackerAsync()
    {
        var removals = await userStatuses.RemoveStatusesByTrackerAsync(trackerId);

        foreach(var contentId in removals.Keys)
            await AlertUserlistUpdate(contentId);
    }

    protected async Task BroadcastToSelfClients(long userId, WebSocketResponse response)
    {
        foreach(var key in currentListeners.Keys.ToList())
        {
            WebsocketListenerData? listener;
            if(currentListeners.TryGetValue(key, out listener))
            {
                if(listener.userId != userId)
                    continue;

                await listener.sendQueue.SendAsync(response);
            }
        }
    }

    //This is VERY inefficient, like oh my goodness, but until it becomes a problem, this is how it'll be.
    //It's inefficient because each user does the status lookup and that's entirely unnecessary.
    protected async Task AlertUserlistUpdate(long contentId)
    {
        foreach(var key in currentListeners.Keys.ToList())
        {
            WebsocketListenerData? listener;
            if(currentListeners.TryGetValue(key, out listener))
            {
                var statuses = await GetUserStatusesAsync(listener.userId, contentId);

                //Note that the listener could be invalid here, but it's OK because after this, hopefully nothing will be 
                //holding onto it or whatever.
                var response = new WebSocketResponse()
                {
                    type = "userlistupdate",
                    data = statuses
                };
                await listener.sendQueue.SendAsync(response);
            }
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
            services.logger.LogDebug($"WS request '{receiveItem.id}'({receiveItem.type}) from {userId}");
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
            else if(receiveItem.type == "selfbroadcast")
            {
                response.data = receiveItem.data;

                await BroadcastToSelfClients(userId, response);

                //This skips the sending of the response, because in a broadcast, you'll receive it anyway
                continue;
            }
            else if(receiveItem.type == "userlist")
            {
                response.data = await GetUserStatusesAsync(userId);
            }
            else if(receiveItem.type == "setuserstatus")
            {
                try
                {
                    if(receiveItem.data == null)
                        throw new RequestException("Must set data to a dictionary of contentId:status");

                    var statuses = (((JObject)receiveItem.data).ToObject<Dictionary<string, string>>() ?? throw new RequestException("Couldn't parse sent userlist!"))
                        .ToDictionary(x => long.Parse(x.Key), y => y.Value);//(Dictionary<long, string>)receiveItem.data;

                    //TODO: this will need to do some magic to send the userlist to everyone. I suppose if I
                    //had a list of all waiters and their send queues.... hmmmm that would actually just work.
                    foreach(var status in statuses)
                        await AddUserStatusAsync(userId, status.Key, status.Value);
                }
                catch(Exception ex)
                {
                    if(!(ex is RequestException))
                        services.logger.LogWarning($"Error when user {userId} set statuses to {receiveItem.data}: {ex}");
                    response.error = $"Error while setting statuses: {ex.Message}";
                }
            }
            else if(receiveItem.type == "request")
            {
                try
                {
                    if(receiveItem.data == null)
                        throw new RequestException("Must provide search criteria for request!");

                    var searchRequest = ((JObject)receiveItem.data).ToObject<SearchRequests>() ?? 
                        throw new RequestException("Couldn't parse search criteria!");

                    var searchResult = await services.searcher.Search(searchRequest, userId);
                    response.data = searchResult;
                }
                catch(Exception ex)
                {
                    response.error = $"Error during search: {ex.Message}";
                }
            }
            else if(receiveItem.type == "write")
            {
                try
                {
                    if(receiveItem.data == null)
                        throw new RequestException("Must provide write item for request!");

                    var writeData = ((JObject)receiveItem.data).ToObject<WebsocketWriteData>() ?? 
                        throw new RequestException("Couldn't parse write data! Must provide type, etc");

                    var writeObject = writeData.@object ?? throw new RequestException("Couldn't parse 'object' before type is known!"); //((JObject)writeData.@object);
                    var type = writeData.type;

                    //This sucks. Wonder if I can make it better
                    if(type == nameof(RequestType.message))
                        response.data = await WriteAsync(writeObject.ToObject<MessageView>() ?? throw new RequestException($"Couldn't parse {type}!"), userId, writeData.activityMessage);
                    else if(type == nameof(RequestType.content))
                        response.data = await WriteAsync(writeObject.ToObject<ContentView>() ?? throw new RequestException($"Couldn't parse {type}!"), userId, writeData.activityMessage);
                    else if(type == nameof(RequestType.user))
                        response.data = await WriteAsync(writeObject.ToObject<UserView>() ?? throw new RequestException($"Couldn't parse {type}!"), userId, writeData.activityMessage);
                    else if(type == nameof(RequestType.uservariable))
                        response.data = await WriteAsync(writeObject.ToObject<UserVariableView>() ?? throw new RequestException($"Couldn't parse {type}!"), userId, writeData.activityMessage);
                    else if(type == nameof(RequestType.watch))
                        response.data = await WriteAsync(writeObject.ToObject<WatchView>() ?? throw new RequestException($"Couldn't parse {type}!"), userId, writeData.activityMessage);
                    else if(type == nameof(RequestType.vote))
                        response.data = await WriteAsync(writeObject.ToObject<VoteView>() ?? throw new RequestException($"Couldn't parse {type}!"), userId, writeData.activityMessage);
                    else if(type == nameof(RequestType.ban))
                        response.data = await WriteAsync(writeObject.ToObject<BanView>() ?? throw new RequestException($"Couldn't parse {type}!"), userId, writeData.activityMessage);
                    else
                        throw new RequestException($"Unknown write type {type}");
                }
                catch(Exception ex)
                {
                    response.error = $"{ex.GetType().Name}: {ex.Message}";
                }
            }
            else
            {
                response.error = $"Unknown request type {receiveItem.type}";
            }

            sendQueue.Post(response);
        }
    }

    protected async Task ListenLoop(CancellationToken cancelToken, int lastId, BufferBlock<object> sendQueue, string token)
    {
        var userId = ValidateToken(token);
        var user = userId == 0 ? new UserView() { id = userId, super = false } : await services.searcher.GetById<UserView>(RequestType.user, userId, true);

        while(!cancelToken.IsCancellationRequested)
        {
            //NOTE: the ReceiveObjectAsync throws an exception on close
            LiveData listenResult;

            listenResult = await eventQueue.ListenAsync(user, lastId, cancelToken);

            userId = ValidateToken(token); // Validate every time
            lastId = listenResult.lastId;

            var response = new WebSocketResponse()
            {
                type = "live",
                data = listenResult,
                requestUserId = userId
            };

            //At this point we're re-validated, so do whatever
            sendQueue.Post(response);
        }
    }

    protected async Task SendLoop(CancellationToken token, WebSocket socket, BufferBlock<object> sendQueue)
    {
        //The only exception this will throw is if they disconnect or something. On disconnect, we'll
        //exit the loop and most likely throw some ClosedException
        while(!token.IsCancellationRequested)
        {
            var sendItem = await sendQueue.ReceiveAsync(token);
            await socket.SendObjectAsync(sendItem, WebSocketMessageType.Text, token);
        }
    }

    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        return new {
            connections = currentListeners.Count,
            queueSize = eventQueue.QueueSize,
            currentTrackerId = trackerId
        };
    }

    [HttpGet("ws")]
    public async Task<ActionResult<string>> WebSocketListenAsync([FromQuery]string token, [FromQuery]int? lastId = null)
    {
        try
        {
            services.logger.LogDebug($"ws METHOD: {HttpContext.Request.Method}, HEADERS: " +
                JsonConvert.SerializeObject(HttpContext.Request.Headers,
                    Formatting.None, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));

            if (!HttpContext.WebSockets.IsWebSocketRequest)
                return BadRequest("You must send a websocket request to this endpoint!");
            
            //None of these will throw exceptions that we can match anyway, so they'll bubble up accordingly...
            //well that might not be entirely true if we're returning 500 errors specifically but... change it if you need.
            using var cancelSource = new CancellationTokenSource();
            using var dualCancel = CancellationTokenSource.CreateLinkedTokenSource(cancelSource.Token, appLifetime.ApplicationStopping, appLifetime.ApplicationStopped);
            var sendQueue = new BufferBlock<object>();
            List<Task> runningTasks = new List<Task>();
            long userId = 0;
            int realLastId = lastId == null ? eventQueue.GetCurrentLastId() : lastId.Value;

            using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            try
            {
                //ALWAYS add the sendloop first so we can process outgoing messages
                runningTasks.Add(SendLoop(dualCancel.Token, socket, sendQueue));

                //You want to keep this validation token thing inside the main exception handler, as ANY of the 
                //below tasks could throw the token validation exception!
                userId = ValidateToken(token);
                services.logger.LogInformation($"Websocket started for user {userId}");

                //ALWAYS send the lastId message, it's basically our "this is the websocket and you're connected"
                var response = new WebSocketResponse()
                {
                    type = "lastId",
                    data = realLastId,
                    requestUserId = userId
                };

                sendQueue.Post(response);

                if(!currentListeners.TryAdd(trackerId, new WebsocketListenerData() { userId = userId, sendQueue = sendQueue }))
                    throw new InvalidOperationException("INTERNAL ERROR: couldn't add you to the listener array!");

                //Can send and receive at the same time, but CAN'T send/receive multiple at the same time.
                runningTasks.Add(ReceiveLoop(dualCancel.Token, socket, sendQueue, token));
                runningTasks.Add(ListenLoop(dualCancel.Token, realLastId, sendQueue, token));

                var completedTask = await Task.WhenAny(runningTasks);
                runningTasks.Remove(completedTask);
                await completedTask; //To throw the exception, if there is one
            }
            catch(OperationCanceledException ex)
            {
                services.logger.LogDebug($"Websocket was cancelled by system, we're probably shutting down: {ex.Message}");

                // ALL should output an operation cancel exception but 
                // I'm ASSUMING that this will specifically not exit until they're ALL done...
                try { await Task.WhenAll(runningTasks); }
                catch (OperationCanceledException) { } //Fine
                catch (Exception exi) { services.logger.LogError($"CRITICAL: EXCEPTION THROWN FROM CANCELED WEBSOCKET WAS UNEXPECTED TYPE: {exi}"); }
                finally { runningTasks.Clear(); }
            }
            catch(TokenException ex)
            {
                //Note: it is OK to use sendQueue even if the sender loop isn't started, because we dump the
                //remaining queue anyway in the finalizer
                services.logger.LogError($"Token exception in websocket: {ex}");
                sendQueue.Post(new WebSocketResponse() { type = "badtoken", error = ex.Message });
            }
            catch(data.ClosedException ex)
            {
                services.logger.LogDebug($"User {userId} closed websocket on their end, this is normal: {ex.Message}");
            }
            //ALl other unhandled exceptions
            catch (Exception ex)
            {
                services.logger.LogError("Unhandled exception in websocket: " + ex.ToString());
                sendQueue.Post(new WebSocketResponse() { type = "unexpected", error = $"Unhandled exception: {ex}" });
            }
            finally
            {
                if(runningTasks.Count > 0)
                {
                    //Cause the cancel source to close naturally after 2 seconds, giving us enough time to send
                    //out remaining messages, but also allowing us to close immediately if everything was already completed
                    //(because we wait on the tasks themselves, which could complete earlier than the cancel)
                    cancelSource.CancelAfter(2000);

                    try { await Task.WhenAll(runningTasks); }
                    catch(ClosedException ex) { services.logger.LogDebug($"Client closed connection manually, this is normal!: {ex.Message}"); }
                    catch(OperationCanceledException ex) { services.logger.LogDebug($"Websocket task cancelled, this is normal: {ex.Message}"); }
                    catch(Exception ex) { services.logger.LogError($"WEBSOCKET CRITICAL: UNHANDLED EXCEPTION DURING CANCEL: {ex}"); }
                }

                if(currentListeners.ContainsKey(trackerId) && !currentListeners.TryRemove(trackerId, out _))
                    services.logger.LogDebug($"Couldn't remove listener {trackerId}, this could be a serious error!");

                //This won't catch errors in every case but do it anyway
                if(socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Force closing due to end of task", dualCancel.Token);
            }
                
            //Just return an empty result if we get all the way to the end. This shouldn't happen but...
            return new EmptyResult();
        }
        finally
        {
            //This is SO IMPORTANT that I want to do it way out here!
            await RemoveStatusesByTrackerAsync();
        }

    }
}