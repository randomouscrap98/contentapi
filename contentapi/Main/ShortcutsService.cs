using AutoMapper;
using contentapi.Search;
using contentapi.data.Views;
using contentapi.data;

namespace contentapi.Main;

public class ShortcutsService
{
    protected IDbServicesFactory dbFactory;
    protected IMapper mapper;
    protected ILogger logger;

    public ShortcutsService(ILogger<ShortcutsService> logger, IDbServicesFactory factory, IMapper mapper)
    {
        this.dbFactory = factory;
        this.logger = logger;
        this.mapper = mapper;
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

    public async Task<VoteView> LookupVoteByContentIdAsync(long uid, long contentId)
    {
        using var search = dbFactory.CreateSearch();
        var votes = await search.SearchSingleType<VoteView>(uid, new SearchRequest()
        {
            type = "vote",
            fields = "*",
            query = "userId = @me and contentId = @cid"
        }, new Dictionary<string, object> {
            { "me", uid },
            { "cid", contentId }
        });

        if (votes.Count == 0)
            throw new NotFoundException($"Content {contentId} not found for vote!");
        
        return votes.First();
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

        var leftovers = messageIds.Except(messages.Select(x => x.id)).ToList();

        if(leftovers.Count > 0)
            throw new RequestException($"(Precheck): Some messages were not found: {string.Join(",", leftovers)}");

        //Get a copy of them but with the parent reset
        var newMessages = messages.Select(x => 
        {
            var m = mapper.Map<MessageView>(x);
            m.contentId = newParent;
            return m;
        }).OrderBy(x => x.id).ToList();
        
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