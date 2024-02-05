using contentapi.Main;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;
using CsvHelper;
using System.Globalization;
using System.Text;
using Dapper;
using contentapi.Live;

namespace contentapi.Controllers;


public class SmallControllerConfig 
{
    public int DefaultPull {get;set;} = 30;
    public TimeSpan UserStatusExpire = TimeSpan.FromSeconds(30);
    public TimeSpan LongPollTimeout = TimeSpan.FromMinutes(5);
}

public class SmallController : BaseController
{
    protected const string CSVMIME = "text/csv";
    protected const string PLAINMIME = "text/plain";

    protected IUserService userService;
    protected IPermissionService permissions;
    protected IUserStatusTracker userStatuses;
    protected IHostApplicationLifetime appLifetime;
    protected ILiveEventQueue eventQueue;
    protected SmallControllerConfig config;
    protected IQueueBackgroundTask backgroundQueue;

        
    public SmallController(BaseControllerServices services, IUserService userService, IPermissionService permissions,
        IUserStatusTracker userStatuses, IHostApplicationLifetime appLifetime, ILiveEventQueue eventQueue,
        SmallControllerConfig config, IQueueBackgroundTask backgroundQueue) : base(services) 
    { 
        this.userService = userService;
        this.permissions = permissions;
        this.appLifetime = appLifetime;
        this.userStatuses = userStatuses;
        this.eventQueue = eventQueue;
        this.backgroundQueue = backgroundQueue;
        this.config = config;
    }

    protected async Task RemoveStatusAfterExpire()
    {
        await Task.Delay(config.UserStatusExpire);
        await userStatuses.RemoveStatusesByTrackerAsync(trackerId);
    }

    protected string GenericStatus(ContentView? content, MessageView? message, UserView? currentUser)
    {
        StringBuilder sb = new();

        if(content != null)
        {
            if (permissions.CanUserStatic(new UserView { id = 0 }, UserAction.read, content.permissions))
            {
                sb.Append('R');
            }
            if (currentUser != null)
            {
                if (permissions.CanUserStatic(currentUser, UserAction.create, content.permissions))
                    sb.Append('P');
                if (content.createUserId == currentUser.id)
                    sb.Append('O');
            }
        }
        if(message != null)
        {
            if(message.edited)
                sb.Append('E');
            if(message.deleted)
                sb.Append('D');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Nearly all result sets from SmallController will be of this "record" type
    /// </summary>
    public class GenericMessageResult
    {
        public string? contentTitle;
        public string? username;
        public string? message;
        public string? datetime;
        public string? module;
        public string? state;
        public long? cid;
        public long? uid;
        public long? mid;
    }

    protected GenericMessageResult MakeGenericMessageResult(ContentView? content, MessageView? message, UserView? user, UserView? currentUser)
    {
        return new GenericMessageResult {
            contentTitle = content?.name,
            username = user?.username,
            message = message?.text,
            datetime = message != null ? Constants.ToCommonDateString(message.createDate) : null,
            module = message?.module,
            state = GenericStatus(content, message, currentUser),
            cid = content?.id,
            uid = user?.id,
            mid = message?.id
        };
    }

    /// <summary>
    /// Return a simple string as plaintext, catching exceptions and returning the appropriate error
    /// </summary>
    /// <param name="work"></param>
    /// <returns></returns>
    protected async Task<ActionResult> SmallTaskCatch(Func<Task<string>> work, string contentType = CSVMIME)
    {
        var result = await MatchExceptions(work);

        if(result.Value == null)
            return result.Result!;
        else 
            return File(System.Text.Encoding.UTF8.GetBytes(result.Value), contentType);
    }

    /// <summary>
    /// Return a list of items as a CSV
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="work"></param>
    /// <returns></returns>
    protected async Task<ActionResult> SmallTaskCatch<T>(Func<Task<List<T>>> work)
    {
        var result = await MatchExceptions(work);

        if(result.Value == null)
        {
            return result.Result!;
        }
        else 
        {
            var csvconfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) {
                HasHeaderRecord = false,
                MemberTypes = CsvHelper.Configuration.MemberTypes.Fields
            };
            using var mstream = new MemoryStream();
            using var writer = new StreamWriter(mstream);
            using var csv = new CsvWriter(writer, csvconfig);
            csv.WriteRecords(result.Value);
            await writer.FlushAsync();
            return File(mstream.ToArray(), CSVMIME);
        }
    }

    [HttpGet("login")]
    public Task<ActionResult> Login([FromQuery]string username, [FromQuery]string password, [FromQuery]long expireSeconds = 0)
    {
        return SmallTaskCatch(async () => 
        {
            RateLimit(RateLogin, username);
            TimeSpan? expireOverride = expireSeconds > 0 ? TimeSpan.FromSeconds(expireSeconds) : null;
            return await userService.LoginUsernameAsync(username, password, expireOverride);
        }, PLAINMIME);
    }

    [Authorize()]
    [HttpGet("me")]
    public Task<ActionResult> Me()
    {
        return SmallTaskCatch(async () => 
        {
            var user = await GetUserViewStrictAsync();
            return new List<(long, string)> {
                (user.id, user.username)
            };
        });
    }

    public class SmallSearch
    {
        public string search {get;set;} = "";
        public long id {get;set;} = 0;
    }

    [HttpGet("search")]
    public Task<ActionResult> Search([FromQuery]SmallSearch searchparam)
    {
        return SmallTaskCatch(async () => 
        {
            UserView user;
            try { user = await GetUserViewStrictAsync(); }
            catch { user = new UserView() { id = 0, username = "DEFAULT"}; }

            List<ContentView> result = new();

            //Construct a search 
            if(searchparam.id != 0)
                result = new List<ContentView> { await CachedSearcher.GetById<ContentView>(RequestType.content, searchparam.id) };
            else if(!string.IsNullOrWhiteSpace(searchparam.search))
                result = await CachedSearcher.GetByField<ContentView>(RequestType.content, nameof(ContentView.name), searchparam.search, "like");
            else
                throw new RequestException("Must supply either id or search");

            return result.Select(x => MakeGenericMessageResult(x, null, null, user)).ToList();
        });
    }

    [Authorize()]
    [HttpGet("post/{id}")]
    public Task<ActionResult> Post([FromRoute]long id, [FromQuery]string message, [FromQuery(Name = "values")]Dictionary<string, string>? values = null)
    {
        return SmallTaskCatch(async () => 
        {
            var user = await GetUserViewStrictAsync();
            var mv = new MessageView {
                text = message,
                contentId = id,
                values = values?.ToDictionary(k=>k.Key, v=>(object)v.Value) ?? new Dictionary<string, object>()
            };
            //This will use the cached writer but that's fine, this particular instance of the smallcontroller will NOT
            //live long, just like the cached values suggest
            var result = await WriteAsync(mv);
            var content = await CachedSearcher.GetById<ContentView>(result.contentId);

            return new List<GenericMessageResult> {
                MakeGenericMessageResult(content, result, user, user)
            };
        });
    }

    public class PollQuery
    {
        public long mid {get;set;} = 0;
        public int eid {get;set;} = 0;
        public int get {get;set;} = 30;
        public List<long> rooms {get;set;} = new List<long>();
        public bool global {get;set;} = false;

        public List<long> UserlistRooms { get {
            var result = new List<long>();
            if(rooms != null)
                result.AddRange(rooms);
            result.Add(0);
            return result;
        }}
    }


    protected async Task<List<GenericMessageResult>> GetUserlistMessages(PollQuery query, long userId)
    {
        using(var searcher = services.dbFactory.CreateSearch())
        {
            var result = await userStatuses.GetUserStatusesAsync(searcher, userId, eventQueue.GetAutoContentRequest().fields, "*", query.UserlistRooms.ToArray());
            var users = searcher.ToStronglyTyped<UserView>(result.objects[nameof(RequestType.user)]);
            return result.statuses.Select(x => new GenericMessageResult(){
                cid = x.Key,
                datetime = Constants.ToCommonDateString(DateTime.Now),
                module = "userlist",
                message = string.Join(", ", x.Value.Keys.Select(uid => users.FirstOrDefault(u => u.id == uid)?.username ?? "???"))
            }).ToList();
        }
    }

    protected async Task<List<GenericMessageResult>> FinalizeMessages(List<GenericMessageResult> messages, PollQuery query, long userId, long eventId)
    {
        messages.AddRange(await GetUserlistMessages(query, userId));
        messages.Add(
            new GenericMessageResult(){
                datetime = Constants.ToCommonDateString(DateTime.Now),
                module = "eventId",
                message = eventId.ToString()
            }
        );
        return messages;
    }

    protected async Task<List<GenericMessageResult>> SearchChatStatic(PollQuery query, UserView currentUser)
    {
        using var searcher = services.dbFactory.CreateSearch();

        var values = new Dictionary<string, object>();
        var requests = new List<SearchRequest>();

        var messageRequest = new SearchRequest()
        {
            type = nameof(RequestType.message),
            fields = "~engagement,values,uidsInText",
            limit = Math.Abs(query.get),
        };

        values.Add("mid", query.mid);

        if (query.get < 0) //Search backwards
        {
            messageRequest.query = "id < @mid";
            messageRequest.order = "id_desc"; //MUST SET order appropriately so we get the right set of messages "less than" mid
        }
        else   //Search forwards
        {
            messageRequest.query = "id > @mid";
            messageRequest.order = "id";
        }

        if (!query.global && query.rooms != null && query.rooms.Count > 0)
        {
            values.Add("rooms", query.rooms);
            messageRequest.query += " and contentId in @rooms";
        }

        var contentRequest = new SearchRequest()
        {
            type = nameof(RequestType.content),
            fields = "id,permissions,name",
            query = "id in @message.contentId",
        };

        var userRequest = new SearchRequest()
        {
            type = nameof(RequestType.user),
            fields = "id,username,avatar",
            query = "id in @message.createUserId"
        };

        requests.Add(messageRequest);
        requests.Add(contentRequest);
        requests.Add(userRequest);

        var result = await searcher.Search(new SearchRequests() { values = values, requests = requests }, currentUser.id);
        var messages = searcher.ToStronglyTyped<MessageView>(result.objects[nameof(RequestType.message)]);
        var content = searcher.ToStronglyTyped<ContentView>(result.objects[nameof(RequestType.content)]);
        var users = searcher.ToStronglyTyped<UserView>(result.objects[nameof(RequestType.user)]);

        if(query.get < 0)
            messages.Reverse();

        // No polling required, immediately return
        return messages.Select(x => MakeGenericMessageResult(
            content.FirstOrDefault(c => c.id == x.contentId),
            x,
            users.FirstOrDefault(u => u.id == x.createUserId),
            currentUser
        )).ToList();
    }

    protected async Task FixPollQuery(PollQuery query)
    {
        //Just in case, throw these away
        if(query.get == 0)
            throw new InvalidOperationException("Amount of messages to 'get' must not be 0!");
        
        if(!query.global && (query.rooms == null || query.rooms.Count == 0))
            throw new InvalidOperationException("You must provide some rooms, or indicate global!");
        
        //We use the permissions service here to check rooms even though we know the permissions will restrict
        //rooms we can't enter simply so that users can't report that they are in rooms they're not allowed in.
        //Also, it prevents people from longpolling on technically "no" rooms
        //if(query.rooms.Any(r => permissions.CanUserStatic()))
        //TODO: apparently the websocket endpoint doesn't restrict this, so neither should this one. If anything
        //is going to restrict it, maybe it can be the user status tracker?
            
        //TODO: this is using the raw tablename!
        if(query.mid <= 0)
        {
            if(query.get < 0)
            {
                //Special case: they're asking for the "last" whatever messages, so mid doesn't have to be queried
                query.mid = long.MaxValue;
            }
            else
            {
                using var rawDb = services.dbFactory.CreateRaw();
                query.mid = await rawDb.ExecuteScalarAsync<long>($"select max(id) from messages"); // - query.mid;
            }
        }
    }

    /// <summary>
    /// Given a query and some position within the live event queue, wait for messages relevant to the query and produce
    /// the fully completed list of messages (finalized)
    /// </summary>
    /// <param name="query"></param>
    /// <param name="currentUser"></param>
    /// <param name="lastId"></param>
    /// <returns></returns>
    protected async Task<List<GenericMessageResult>> ListenChat(PollQuery query, UserView currentUser, int lastId)
    {
        try
        {
            //Step 2: wait on the listening endpoint. Since we're now "polling", go ahead and report the user's 
            //requested list of rooms 
            if (query.rooms != null)
                foreach (var cid in query.UserlistRooms)
                    await userStatuses.AddStatusAsync(currentUser.id, cid, "active", trackerId);

            //Step 3: listen on the live events until you get a response in one of the rooms.
            var result = new List<GenericMessageResult>();

            using var fullSource = CancellationTokenSource.CreateLinkedTokenSource(appLifetime.ApplicationStopping, appLifetime.ApplicationStopped);
            fullSource.CancelAfter(config.LongPollTimeout);

            try
            {
                using var searcher = services.dbFactory.CreateSearch(); //Is it better to keep recreating this, or just once? Probably just once...

                while (!fullSource.IsCancellationRequested)
                {
                    var listenData = await eventQueue.ListenAsync(currentUser, lastId, fullSource.Token);
                    lastId = listenData.lastId;

                    //Nothing to do, don't waste time
                    if (!listenData.events.Any(x => x.type == nameof(EventType.message_event)))
                        continue;

                    var content = searcher.ToStronglyTyped<ContentView>(listenData.objects[EventType.message_event][nameof(RequestType.content)]);
                    var messages = searcher.ToStronglyTyped<MessageView>(listenData.objects[EventType.message_event][nameof(RequestType.message)]);
                    var users = searcher.ToStronglyTyped<UserView>(listenData.objects[EventType.message_event][nameof(RequestType.user)]);

                    //We only care about message events (for now anyway), and only the ones which are in our content group
                    foreach (var ev in listenData.events.Where(x => x.type == nameof(EventType.message_event)))
                    {
                        //Skip messages within content we didn't request. No need to check perms btw, since the listener
                        //does this for us.
                        if (query.rooms != null && query.rooms.Count > 0 && !query.rooms.Contains(ev.contentId))
                            continue;

                        var message = messages.FirstOrDefault(x => x.id == ev.refId);

                        //So for this event, we need to pull the message, content, and I guess the create user?
                        result.Add(MakeGenericMessageResult(
                            content.FirstOrDefault(x => x.id == ev.contentId),
                            message,
                            users.FirstOrDefault(x => x.id == message?.createUserId),
                            currentUser
                        ));
                    }

                    if (result.Any())
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                services.logger.LogDebug($"Small longpolling cancelled (normal)");
            }

            return await FinalizeMessages(result, query, currentUser.id, lastId);
        }
        finally
        {
            backgroundQueue.AddTask(RemoveStatusAfterExpire());
        }

    }

    [Authorize()]
    [HttpGet("chat")]
    public Task<ActionResult> Chat([FromQuery]PollQuery query)
    {
        return SmallTaskCatch(async () => 
        {
            await FixPollQuery(query);
            var currentUser = await GetUserViewStrictAsync();

            //Step 0: before doing ANYTHING, must retrieve the maxId in the event system. This way, if it increases 
            //while we spend time looking for the non-polling data, we will catch everything
            var lastId = query.eid > 0 ? query.eid : eventQueue.GetCurrentLastId();

            //Step 1: query the normal way for results. Do NOT use any cached stuff here!
            var staticResult = await SearchChatStatic(query, currentUser);

            if(staticResult.Any())
                return await FinalizeMessages(staticResult, query, currentUser.id, lastId); 
            else
                return await ListenChat(query, currentUser, lastId);
        });
    }

}