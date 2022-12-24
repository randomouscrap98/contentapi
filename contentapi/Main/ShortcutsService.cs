using AutoMapper;
using contentapi.Search;
using contentapi.data.Views;
using contentapi.data;

namespace contentapi.Main;

public class ShortcutsService
{
    protected IDbServicesFactory dbFactory;
    protected IViewTypeInfoService typeInfoService;
    protected IMapper mapper;
    protected ILogger logger;

    public const string RethreadKey = "rethread";
    public const string OriginalContentIdKey = "originalContentId";
    public const string StartIdentifier = "start";
    public const string EndIdentifier = "end";

    public ShortcutsService(ILogger<ShortcutsService> logger, IDbServicesFactory factory, IViewTypeInfoService typeInfoService, IMapper mapper)
    {
        this.dbFactory = factory;
        this.logger = logger;
        this.mapper = mapper;
        this.typeInfoService = typeInfoService;
    }

    public async Task ClearNotificationsAsync(WatchView watch, long uid)
    {
        using var search = dbFactory.CreateSearch();
        var getRequest = new Func<string, SearchRequest>(t => new SearchRequest()
        {
            type = t,
            fields = "id,contentId",
            query = "contentId = @cid",
            order = "id_desc",
            limit = 1
        });
        var getValues = new Func<Dictionary<string, object>>(() => new Dictionary<string, object> {
            { "cid", watch.contentId }
        });

        //Need the latest values for comment and activity. Don't care if message is a module
        //message, the id will still be valid for tracking purposes
        var messages = await search.SearchSingleType<MessageView>(uid, getRequest("message"), getValues());
        var activity = await search.SearchSingleType<ActivityView>(uid, getRequest("activity"), getValues());

        if (messages.Count > 0)
            watch.lastCommentId = messages.First().id;
        if (activity.Count > 0)
            watch.lastActivityId = activity.First().id;
    }

    public async Task<WatchView> LookupWatchByContentIdAsync(long uid, long contentId)
    {
        using var search = dbFactory.CreateSearch();
        var watches = await search.SearchSingleType<WatchView>(uid, new SearchRequest()
        {
            type = "watch",
            fields = $"~{nameof(WatchView.activityNotifications)},{nameof(WatchView.commentNotifications)}", 
            query = "userId = @me and contentId = @cid"
        }, new Dictionary<string, object> {
            { "me", uid },
            { "cid", contentId }
        });

        if (watches.Count == 0)
            throw new NotFoundException($"Content {contentId} not found for watch!");
        
        return watches.First();
    }

    public async Task<T> LookupEngagementByRelatedIdAsync<T>(long uid, long relatedId, string type) where T : IEngagementView
    {
        using var search = dbFactory.CreateSearch();
        string query; Dictionary<string, object> objects;
        search.GetEngagementLookup(uid, relatedId, type, out query, out objects);

        var typeInfo = typeInfoService.GetTypeInfo<T>();
        var requestType = typeInfo.requestType ?? throw new InvalidOperationException($"Tried to lookup engagement view {typeof(T)} but it had no requestType!");
        
        var engagement = await search.SearchSingleType<T>(uid, new SearchRequest()
        {
            type = requestType.ToString(),
            fields = "*",
            query = query
        }, objects);

        if (engagement.Count == 0)
            throw new NotFoundException($"Parent {relatedId} not found for engagement!");
        
        return engagement.First();
    }

    public async Task<UserVariableView> LookupVariableByKeyAsync(long uid, string key)
    {
        using var search = dbFactory.CreateSearch();
        var variables = await search.SearchSingleType<UserVariableView>(uid, new SearchRequest()
        {
            type = "uservariable",
            fields = "*",
            query = "userId = @me and key = @key"
        }, new Dictionary<string, object> {
            { "me", uid },
            { "key", key }
        });

        if (variables.Count == 0)
            throw new NotFoundException($"Variable {key} not found!");
        
        return variables.First();
    }

    public class RethreadMeta 
    {
        public DateTime date {get;set;} = DateTime.UtcNow;
        public int count {get;set;}
        public string position {get;set;} = "";
        public long lastContentId {get;set;}
    }

    /// <summary>
    /// Move all the given messages (by id) to the given parent. Fails early if any message is not found, or a problem is found
    /// with permissions before the move. May still fail in the middle.
    /// </summary>
    /// <param name="messageIds"></param>
    /// <param name="newParent"></param>
    /// <param name="requester"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task<List<MessageView>> RethreadMessagesAsync(List<long> messageIds, long newParent, long requester, string message = "")
    {
        using var search = dbFactory.CreateSearch();
        using var writer = dbFactory.CreateWriter();
        var user = await search.GetById<UserView>(RequestType.user, requester, true);

        if(string.IsNullOrWhiteSpace(message))
            message = $"Rethreading {messageIds.Count} comments to content {newParent}";
        
        //Just do nothing so we don't have to worry about empty!
        if(messageIds.Count == 0)
            return new List<MessageView>();

        //Go lookup the messages
        var messages = await search.SearchSingleType<MessageView>(requester, new SearchRequest()
        {
            type = RequestType.message.ToString(),
            fields = "*",
            query = "id in @ids",
            order = "id"
        }, new Dictionary<string, object>()
        {
            { "ids", messageIds }
        });

        //Find ids that weren't in the resultset; if there are any, the rethread fails early
        var leftovers = messageIds.Except(messages.Select(x => x.id)).ToList();

        if(leftovers.Count > 0)
            throw new RequestException($"(Precheck): Some messages were not found: {string.Join(",", leftovers)}");
        
        //WARN: Assume all messages come from the same content!
        var oldContentId = messages.First().contentId;

        //Get a copy of them but with the parent reset
        var newMessages = messages.Select(x => 
        {
            var m = mapper.Map<MessageView>(x);
            //If this is the first rethread for this message, stamp it with the original contentId it was posted in
            if(!m.values.ContainsKey(OriginalContentIdKey))
                m.values.Add(OriginalContentIdKey, x.contentId);
            m.contentId = newParent;
            return m;
        }).OrderBy(x => x.id).ToList();

        var addMeta = new Action<MessageView, string>((view,position) => {
            //We overwrite previous metadata, so to make that easier, always add the key
            //(so whether it existed or not, it's fine)
            if(!view.values.ContainsKey(RethreadKey))
                view.values.Add(RethreadKey, true); //Placeholder value
            view.values[RethreadKey] = new RethreadMeta { 
                date = DateTime.UtcNow,
                position = position,
                count = newMessages.Count,
                lastContentId = oldContentId //DON'T use the view, they already have the contentId set!
            };
        });

        //Stamp the first and last messages (or the singular message) with rethread metadata
        if(newMessages.Count == 1)
        {
            addMeta(newMessages.First(), $"{StartIdentifier}|{EndIdentifier}");
        }
        else
        {
            addMeta(newMessages.First(), StartIdentifier);
            addMeta(newMessages.Last(), EndIdentifier);
        }
        
        //Now, verify them all. If ANY of them throw an error, it will happen BEFORE work was performed
        for(var i = 0; i < newMessages.Count; i++) 
            await writer.ValidateWorkGeneral(newMessages[i], messages[i], user, UserAction.update);
        
        //OK, now we can write them
        var result = new List<MessageView>();
        foreach(var m in newMessages)
            result.Add(await writer.WriteAsync(m, requester, message));

        await writer.WriteAdminLog(new Db.AdminLog()
        {
            type = AdminLogType.rethread,
            initiator = requester,
            target = newParent,
            text = $"User {user.username} ({user.id}) rethreaded {result.Count} messages into content {newParent}" 
        });

        return result;
    }
}