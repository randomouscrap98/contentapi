using contentapi.Main;
using contentapi.Search;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;


[Authorize()]
public class WriteController : BaseController
{
    public WriteController(BaseControllerServices services) : base(services) { }

    [HttpPost("message")]
    public Task<ActionResult<MessageView>> WriteMessageAsync([FromBody]MessageView message) =>
        MatchExceptions(() => WriteAsync(message));

    [HttpPost("content")]
    public Task<ActionResult<ContentView>> WriteContentAsync([FromBody]ContentView page, [FromQuery]string? activityMessage) =>
        MatchExceptions(() => WriteAsync(page, null, activityMessage));

    //This is SLIGHTLY special in that user writes are mostly for self updates... but might be used for new groups as well? You also
    //can't update PRIVATE data through this endpoint
    [HttpPost("user")]
    public Task<ActionResult<UserView>> WriteUserAsync([FromBody]UserView user) =>
        MatchExceptions(() => WriteAsync(user));

    [HttpPost("watch")]
    public Task<ActionResult<WatchView>> WriteWatchAsync([FromBody]WatchView watch) =>
        MatchExceptions(() => WriteAsync(watch));

    [HttpPost("content_engagement")]
    public Task<ActionResult<ContentEngagementView>> WriteContentEngagementAsync([FromBody]ContentEngagementView engagement) =>
        MatchExceptions(() => WriteAsync(engagement));

    [HttpPost("message_engagement")]
    public Task<ActionResult<MessageEngagementView>> WriteMessageEngagementAsync([FromBody]MessageEngagementView engagement) =>
        MatchExceptions(() => WriteAsync(engagement));

    [HttpPost("uservariable")]
    public Task<ActionResult<UserVariableView>> WriteUserVariableAsync([FromBody]UserVariableView variable) =>
        MatchExceptions(() => WriteAsync(variable));

    [HttpPost("ban")]
    public Task<ActionResult<BanView>> WriteBanVariableAsync([FromBody]BanView ban) =>
        MatchExceptions(() => WriteAsync(ban));

    [HttpPost("userrelation")]
    public Task<ActionResult<UserRelationView>> WriteUserRelationVariableAsync([FromBody]UserRelationView userRelation) =>
        MatchExceptions(() => WriteAsync(userRelation));
}