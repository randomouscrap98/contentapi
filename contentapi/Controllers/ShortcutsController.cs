using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

            return await services.writer.WriteAsync(watch, uid); //message used for activity and such
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

            return await services.writer.DeleteAsync<WatchView>(watch.id, uid); //message used for activity and such
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

            return await services.writer.WriteAsync(watch, uid); //message used for activity and such
        });
    }
}