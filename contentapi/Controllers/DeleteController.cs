using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;


[Authorize()]
public class DeleteController : BaseController
{
    public DeleteController(BaseControllerServices services) : base(services) { }

    [HttpPost("message/{id}")]
    public Task<ActionResult<MessageView>> DeleteCommentAsync([FromRoute]long id) //, [FromBody]string? message = null)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            return await CachedWriter.DeleteAsync<MessageView>(id, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("content/{id}")]
    public Task<ActionResult<ContentView>> DeleteContentAsync([FromRoute]long id, [FromQuery]string? message = null)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            return await CachedWriter.DeleteAsync<ContentView>(id, GetUserIdStrict(), message); //message used for activity and such
        });
    }

    //Again slightly special since deleting users is GENERALLY um... not what we want to do?
    [HttpPost("user/{id}")]
    public Task<ActionResult<UserView>> DeleteUserAsync([FromRoute]long id) //, [FromBody]string? message = null)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            return await CachedWriter.DeleteAsync<UserView>(id, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("watch/{id}")]
    public Task<ActionResult<WatchView>> DeleteWatchAsync([FromRoute]long id)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract); //watches are different I guess?
            return await CachedWriter.DeleteAsync<WatchView>(id, GetUserIdStrict()); 
        });
    }

    [HttpPost("vote/{id}")]
    public Task<ActionResult<VoteView>> DeleteVoteAsync([FromRoute]long id)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract); //watches are different I guess?
            return await CachedWriter.DeleteAsync<VoteView>(id, GetUserIdStrict()); 
        });
    }

    [HttpPost("uservariable/{id}")]
    public Task<ActionResult<UserVariableView>> DeleteUserVariableAsync([FromRoute]long id)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateUserVariable);
            return await CachedWriter.DeleteAsync<UserVariableView>(id, GetUserIdStrict()); 
        });
    }
}