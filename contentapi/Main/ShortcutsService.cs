using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;

namespace contentapi.Main;

public class ShortcutsService
{
    protected IDbWriter writer;
    protected IGenericSearch search;
    protected ILogger logger;

    public ShortcutsService(ILogger<ShortcutsService> logger, IDbWriter writer, IGenericSearch search)
    {
        this.writer = writer;
        this.search = search;
        this.logger = logger;
    }

    public async Task ClearNotificationsAsync(WatchView watch, long uid)
    {
        //Need the latest values for comment and activity. Don't care if message is a module
        //message, the id will still be valid for tracking purposes
        var messages = await search.SearchSingleType<MessageView>(uid, new SearchRequest()
        {
            type = "message",
            fields = "id",
            order = "id_desc",
            limit = 1
        });

        var activity = await search.SearchSingleType<ActivityView>(uid, new SearchRequest()
        {
            type = "activity",
            fields = "id",
            order = "id_desc",
            limit = 1
        });

        if (messages.Count > 0)
            watch.lastCommentId = messages.First().id;
        if (activity.Count > 0)
            watch.lastActivityId = activity.First().id;
    }

    public async Task<WatchView> LookupWatchByContentIdAsync(long uid, long contentId)
    {
        var watches = await search.SearchSingleType<WatchView>(uid, new SearchRequest()
        {
            type = "watch",
            fields = "id, contentId",
            query = "userId = @me and contentId = @cid"
        }, new Dictionary<string, object> {
            { "me", uid },
            { "cid", contentId }
        });

        if (watches.Count == 0)
            throw new NotFoundException($"Content {contentId} not found for watch!");
        
        return watches.First();
    }
}