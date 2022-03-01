using contentapi.Main;
using contentapi.Search;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;


[Authorize()]
public class WriteController : BaseController
{
    protected IDbWriter writer;
    protected IGenericSearch searcher;

    public WriteController(BaseControllerServices services, IGenericSearch search, IDbWriter writer) : base(services)
    {
        this.searcher = search;
        this.writer = writer;
    }

    [HttpPost("message")]
    public Task<ActionResult<MessageView>> WriteMessageAsync([FromBody]MessageView message) =>
        MatchExceptions(async () => await writer.WriteAsync(message, GetUserIdStrict())); //message used for activity and such

    [HttpPost("content")]
    public Task<ActionResult<ContentView>> WriteContentAsync([FromBody]ContentView page, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.WriteAsync(page, GetUserIdStrict(), activityMessage)); //message used for activity and such

    //This is SLIGHTLY special in that user writes are mostly for self updates... but might be used for new groups as well? You also
    //can't update PRIVATE data through this endpoint
    [HttpPost("user")]
    public Task<ActionResult<UserView>> WriteUserAsync([FromBody]UserView user) =>
        MatchExceptions(async () => await writer.WriteAsync(user, GetUserIdStrict())); //message used for activity and such


}