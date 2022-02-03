using System.Diagnostics;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

//public class RequestResponse : GenericSearchResult
//{
//    public double totalTime {get;set;}
//    public double nonDbTime {get;set;}
//    public long? requestUser {get;set;}
//}
//
//public class RequestResponseProfile : Profile
//{
//    public RequestResponseProfile()
//    {
//        CreateMap<GenericSearchResult, RequestResponse>().ReverseMap();
//    }
//}

[Authorize()]
public class WriteController : BaseController
{
    protected IDbWriter writer;
    protected IGenericSearch searcher;
    //protected IQueryBuilder queryBuilder;

    public WriteController(BaseControllerServices services, IGenericSearch search,
        IDbWriter writer) : base(services)
    {
        this.searcher = search;
        this.writer = writer;
        //this.queryBuilder = queryBuilder;
    }

    [HttpPost("comment")]
    public Task<ActionResult<CommentView>> WriteCommentAsync([FromBody]CommentView comment) =>
        MatchExceptions(async () => await writer.WriteAsync(comment, GetUserIdStrict())); //message used for activity and such

    [HttpPost("page")]
    public Task<ActionResult<PageView>> WritePageAsync([FromBody]PageView page, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.WriteAsync(page, GetUserIdStrict(), activityMessage)); //message used for activity and such

    [HttpPost("file")]
    public Task<ActionResult<FileView>> WriteFileAsync([FromBody]FileView file, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.WriteAsync(file, GetUserIdStrict(), activityMessage)); //message used for activity and such

    [HttpPost("module")]
    public Task<ActionResult<ModuleView>> WriteModuleAsync([FromBody]ModuleView module, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.WriteAsync(module, GetUserIdStrict(), activityMessage)); //message used for activity and such


    //This is SLIGHTLY special in that user writes are mostly for self updates... but might be used for new groups as well? You also
    //can't update PRIVATE data through this endpoint
    [HttpPost("user")]
    public Task<ActionResult<UserView>> WriteUserAsync([FromBody]UserView user) =>
        MatchExceptions(async () => await writer.WriteAsync(user, GetUserIdStrict())); //message used for activity and such


    [HttpPost("comment/delete/{id}")]
    public Task<ActionResult<CommentView>> DeleteCommentAsync([FromRoute]long id) =>
        MatchExceptions(async () => await writer.DeleteAsync<CommentView>(id, GetUserIdStrict())); //message used for activity and such

    [HttpPost("page/delete/{id}")]
    public Task<ActionResult<PageView>> DeletePageAsync([FromRoute]long id, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.DeleteAsync<PageView>(id, GetUserIdStrict(), activityMessage)); //message used for activity and such

    [HttpPost("file/delete/{id}")]
    public Task<ActionResult<FileView>> DeleteFileAsync([FromRoute]long id, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.DeleteAsync<FileView>(id, GetUserIdStrict(), activityMessage)); //message used for activity and such

    [HttpPost("module/delete/{id}")]
    public Task<ActionResult<ModuleView>> DeleteModuleAsync([FromRoute]long id, [FromQuery]string? activityMessage) =>
        MatchExceptions(async () => await writer.DeleteAsync<ModuleView>(id, GetUserIdStrict(), activityMessage)); //message used for activity and such


    //Again slightly special since deleting users is GENERALLY um... not what we want to do?
    [HttpPost("user/delete/{id}")]
    public Task<ActionResult<UserView>> DeleteUserAsync([FromRoute]long id) =>
        MatchExceptions(async () => await writer.DeleteAsync<UserView>(id, GetUserIdStrict())); //message used for activity and such

    //[HttpPost()]
    //public Task<ActionResult<RequestResponse>> RequestAsync([FromBody]SearchRequests search)
    //{
    //    var sw = new Stopwatch();
    //    sw.Start();

    //    return MatchExceptions(async () =>
    //    {
    //        var data = await searcher.Search(search, GetUserId() ?? 0);
    //        var result = services.mapper.Map<RequestResponse>(data);
    //        result.requestUser = GetUserId();
    //        sw.Stop();
    //        result.totalTime = sw.Elapsed.TotalMilliseconds;
    //        result.nonDbTime = result.totalTime - result.databaseTimes.Sum(x => x.Value);

    //        return result;
    //    });
    //}

    //[HttpGet("about")]
    //public ActionResult<object> About()
    //{
    //    return new {
    //        about = "The 'request' endpoint replaces the chainer and longpoller endpoints from the previous api. " +
    //                "Because of the complexity of the request format, you now must POST to get results. " +
    //                "You can request data from any 'table', and the query is in a kind of SQL format rather than " +
    //                "search objects. You must name each request, because you can reference requests in later " +
    //                "requests to perform 'chaining'. You can use values in your query, but they must be parameters " +
    //                "in the form of @value. For examples, please see the unit tests in github: https://github.com/randomouscrap98/contentapi/blob/master/contentapi.test/Search/GenericSearchDbTests.cs",
    //        details = queryBuilder.GetAboutSearch()
    //    };
    //}
}