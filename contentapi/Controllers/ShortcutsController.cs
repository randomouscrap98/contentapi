using contentapi.Main;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;

namespace contentapi.Controllers;


[Authorize()]
public class ShortcutsController : BaseController
{
    protected ShortcutsService shortcuts;

    public ShortcutsController(BaseControllerServices services, ShortcutsService service) : base(services) 
    { 
        this.shortcuts = service;
    }

    [HttpPost("watch/add/{contentId}")]
    public Task<ActionResult<WatchView>> AddWatch([FromRoute]long contentId)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract);
            var uid = GetUserIdStrict();

            //Now construct the watch
            var watch = new WatchView() {
                contentId = contentId
            };

            await shortcuts.ClearNotificationsAsync(watch, uid);

            return await CachedWriter.WriteAsync(watch, uid); //message used for activity and such
        });
    }

    [HttpPost("watch/delete/{contentId}")]
    public Task<ActionResult<WatchView>> DeleteWatch([FromRoute]long contentId)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract);

            var uid = GetUserIdStrict();
            var watch = await shortcuts.LookupWatchByContentIdAsync(uid, contentId);

            return await CachedWriter.DeleteAsync<WatchView>(watch.id, uid); //message used for activity and such
        });
    }

    [HttpPost("watch/clear/{contentId}")]
    public Task<ActionResult<WatchView>> ClearWatch([FromRoute]long contentId)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract);

            var uid = GetUserIdStrict();
            var watch = await shortcuts.LookupWatchByContentIdAsync(uid, contentId);
            await shortcuts.ClearNotificationsAsync(watch, uid);

            return await CachedWriter.WriteAsync(watch, uid); //message used for activity and such
        });
    }

    protected Task<ActionResult<T>> AddEngagement<T>(long relatedId, string type, string? engagement) where T : class, IEngagementView, new()
    {
        //A nice shortcut in case users do it this way
        if(engagement == null)
            return DeleteEngagement<T>(relatedId, type);

        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract);
            var uid = GetUserIdStrict();

            T writeEngagement;

            //Try to lookup the existing vote to update it, otherwise create a new one
            try
            {
                writeEngagement = await shortcuts.LookupEngagementByRelatedIdAsync<T>(uid, relatedId, type);
            }
            catch(NotFoundException)
            {
                writeEngagement = new T() {
                    type = type
                };

                writeEngagement.SetRelatedId(relatedId);
            }

            //Actually set the vote to what they wanted
            writeEngagement.engagement = engagement;

            return await CachedWriter.WriteAsync(writeEngagement, uid);
        });
    }

    protected Task<ActionResult<T>> DeleteEngagement<T>(long relatedId, string type) where T : class, IEngagementView, new()
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract);

            var uid = GetUserIdStrict();
            var engagement = await shortcuts.LookupEngagementByRelatedIdAsync<T>(uid, relatedId, type);

            return await CachedWriter.DeleteAsync<T>(engagement.id, uid);
        });
    }


    [HttpPost("content/{contentId}/setengagement/{type}")]
    public Task<ActionResult<ContentEngagementView>> AddContentEngagement([FromRoute]long contentId, [FromRoute]string type, [FromBody]string? engagement) =>
        AddEngagement<ContentEngagementView>(contentId, type, engagement);

    [HttpPost("message/{messageId}/setengagement/{type}")]
    public Task<ActionResult<MessageEngagementView>> AddMessageEngagement([FromRoute]long messageId, [FromRoute]string type, [FromBody]string? engagement) =>
        AddEngagement<MessageEngagementView>(messageId, type, engagement);

    [HttpPost("content/{contentId}/deleteengagement/{type}")]
    public Task<ActionResult<ContentEngagementView>> DeleteContentEngagement([FromRoute]long contentId, [FromRoute]string type) =>
        DeleteEngagement<ContentEngagementView>(contentId, type);

    [HttpPost("message/{messageId}/deleteengagement/{type}")]
    public Task<ActionResult<MessageEngagementView>> DeleteMessageEngagement([FromRoute]long messageId, [FromRoute]string type) =>
        DeleteEngagement<MessageEngagementView>(messageId, type);

    [HttpGet("uservariable/{key}")]
    public Task<ActionResult<string>> GetUserVariable([FromRoute]string key)
    {
        return MatchExceptions(async () =>
        {
            var uid = GetUserIdStrict();
            var variable = await shortcuts.LookupVariableByKeyAsync(uid, key);

            return variable.value;
        });
    }

    [HttpPost("uservariable/delete/{key}")]
    public Task<ActionResult<UserVariableView>> DeleteUserVariable([FromRoute]string key)
    {
        return MatchExceptions(async () =>
        {
            var uid = GetUserIdStrict();
            var variable = await shortcuts.LookupVariableByKeyAsync(uid, key);
            return await CachedWriter.DeleteAsync<UserVariableView>(variable.id, uid);
        });
    }

    [HttpPost("uservariable/{key}")]
    public Task<ActionResult<UserVariableView>> SetUserVariable([FromRoute]string key, [FromBody]string value)
    {
        return MatchExceptions(async () =>
        {
            RateLimit(RateUserVariable);

            var uid = GetUserIdStrict();

            UserVariableView variable;

            try
            {
                //Get existing variable, set value
                variable = await shortcuts.LookupVariableByKeyAsync(uid, key);
                variable.value = value;
            }
            catch(NotFoundException)
            {
                //Make new variable, set value/key
                variable = new UserVariableView() {
                    key = key,
                    value = value
                };
            }

            return await CachedWriter.WriteAsync(variable, uid);
        });
    }

    public class RethreadData
    {
        public List<long> messageIds {get;set;} = new List<long>();
        public long contentId {get;set;}
        public string message {get;set;} = "";
    }

    [HttpPost("rethread")]
    public Task<ActionResult<List<MessageView>>> RethreadAsync([FromBody]RethreadData rethread)
    {
        return MatchExceptions(async () =>
        {
            RateLimit(RateWrite);
            var uid = GetUserIdStrict();

            if(rethread.contentId == 0)
                throw new RequestException("Must set contentId!");
            if(rethread.messageIds.Count == 0)
                throw new RequestException("Must set at least one messageId!");
            
            return await shortcuts.RethreadMessagesAsync(rethread.messageIds, rethread.contentId, uid, rethread.message);
        });
    }

    public class SimpleBanData
    {
        public long bannedUserId {get;set;}
        public BanType type {get;set;}
        public double banHours {get;set;}
        public string? message {get;set;}
    }

    [HttpPost("ban")]
    public Task<ActionResult<BanView>> AddBanView([FromBody]SimpleBanData banData)
    {
        return MatchExceptions(async () =>
        {
            var uid = GetUserIdStrict();
            var realBan = new BanView()
            {
                bannedUserId = banData.bannedUserId,
                type = banData.type,
                expireDate = DateTime.UtcNow.AddHours(banData.banHours),
                message = banData.message
            };
            return await CachedWriter.WriteAsync(realBan, uid);
        });
    }
}