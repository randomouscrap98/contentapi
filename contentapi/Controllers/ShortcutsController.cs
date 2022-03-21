using contentapi.Db;
using contentapi.Main;
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

    [HttpPost("vote/set/{contentId}")]
    public Task<ActionResult<VoteView>> AddVote([FromRoute]long contentId, [FromBody]VoteType vote)
    {
        //A nice shortcut in case users do it this way
        if(vote == VoteType.none)
            return DeleteVote(contentId);

        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract);
            var uid = GetUserIdStrict();

            VoteView writeVote;

            //Try to lookup the existing vote to update it, otherwise create a new one
            try
            {
                writeVote = await shortcuts.LookupVoteByContentIdAsync(uid, contentId);
            }
            catch(NotFoundException)
            {
                writeVote = new VoteView() {
                    contentId = contentId
                };
            }

            //Actually set the vote to what they wanted
            writeVote.vote = vote;

            return await services.writer.WriteAsync(writeVote, uid); //message used for activity and such
        });
    }

    [HttpPost("vote/delete/{contentId}")]
    public Task<ActionResult<VoteView>> DeleteVote([FromRoute]long contentId)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract);

            var uid = GetUserIdStrict();
            var vote = await shortcuts.LookupVoteByContentIdAsync(uid, contentId);

            return await services.writer.DeleteAsync<VoteView>(vote.id, uid); //message used for activity and such
        });
    }
}