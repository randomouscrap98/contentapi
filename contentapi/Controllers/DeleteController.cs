using contentapi.Main;
using contentapi.Search;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;


[Authorize()]
public class DeleteController : BaseController
{
    public DeleteController(BaseControllerServices services) : base(services) { }

    [HttpPost("message/{id}")]
    public Task<ActionResult<MessageView>> DeleteCommentAsync([FromRoute]long id)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            return await services.writer.DeleteAsync<MessageView>(id, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("content/{id}")]
    public Task<ActionResult<ContentView>> DeleteContentAsync([FromRoute]long id, [FromQuery]string? activityMessage)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            return await services.writer.DeleteAsync<ContentView>(id, GetUserIdStrict(), activityMessage); //message used for activity and such
        });
    }

    //Again slightly special since deleting users is GENERALLY um... not what we want to do?
    [HttpPost("user/{id}")]
    public Task<ActionResult<UserView>> DeleteUserAsync([FromRoute]long id)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            return await services.writer.DeleteAsync<UserView>(id, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("watch/{id}")]
    public Task<ActionResult<WatchView>> DeleteWatchAsync([FromRoute]long id)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract); //watches are different I guess?
            return await services.writer.DeleteAsync<WatchView>(id, GetUserIdStrict()); //message used for activity and such
        });
    }
}