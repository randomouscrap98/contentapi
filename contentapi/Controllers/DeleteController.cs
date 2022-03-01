using contentapi.Main;
using contentapi.Search;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;


[Authorize()]
public class DeleteController : BaseController
{
    protected IDbWriter writer;
    protected IGenericSearch searcher;

    public DeleteController(BaseControllerServices services, IGenericSearch search, IDbWriter writer) : base(services)
    {
        this.searcher = search;
        this.writer = writer;
    }

    [HttpPost("message/{id}")]
    public Task<ActionResult<MessageView>> DeleteCommentAsync([FromRoute]long id) =>
        MatchExceptions(async () => await writer.DeleteAsync<MessageView>(id, GetUserIdStrict())); //message used for activity and such

    [HttpPost("page/{id}")]
    public Task<ActionResult<ContentView>> DeleteContentAsync([FromRoute]long id, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.DeleteAsync<ContentView>(id, GetUserIdStrict(), activityMessage)); //message used for activity and such

    //[HttpPost("page/{id}")]
    //public Task<ActionResult<PageView>> DeletePageAsync([FromRoute]long id, [FromQuery]string? activityMessage) =>
    //    MatchExceptions(async () => await writer.DeleteAsync<PageView>(id, GetUserIdStrict(), activityMessage)); //message used for activity and such

    //[HttpPost("file/{id}")]
    //public Task<ActionResult<FileView>> DeleteFileAsync([FromRoute]long id, [FromQuery]string? activityMessage) =>
    //    MatchExceptions(async () => await writer.DeleteAsync<FileView>(id, GetUserIdStrict(), activityMessage)); //message used for activity and such

    //[HttpPost("module/{id}")]
    //public Task<ActionResult<ModuleView>> DeleteModuleAsync([FromRoute]long id, [FromQuery]string? activityMessage) =>
    //    MatchExceptions(async () => await writer.DeleteAsync<ModuleView>(id, GetUserIdStrict(), activityMessage)); //message used for activity and such


    //Again slightly special since deleting users is GENERALLY um... not what we want to do?
    [HttpPost("user/{id}")]
    public Task<ActionResult<UserView>> DeleteUserAsync([FromRoute]long id) =>
        MatchExceptions(async () => await writer.DeleteAsync<UserView>(id, GetUserIdStrict())); //message used for activity and such
}