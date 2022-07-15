using System.Diagnostics;
using AutoMapper;
using contentapi.data;
using contentapi.Search;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class RequestResponse : GenericSearchResult
{
    public double totalTime {get;set;}
    public double nonDbTime {get;set;}
    public long? requestUser {get;set;}
}

public class RequestResponseProfile : Profile
{
    public RequestResponseProfile()
    {
        CreateMap<GenericSearchResult, RequestResponse>().ReverseMap();
    }
}

public class RequestController : BaseController
{
    protected IQueryBuilder queryBuilder;

    public RequestController(BaseControllerServices services, IQueryBuilder queryBuilder) : base(services)
    {
        this.queryBuilder = queryBuilder;
    }

    [HttpPost()]
    public Task<ActionResult<RequestResponse>> RequestAsync([FromBody]SearchRequests search)
    {
        var sw = new Stopwatch();
        sw.Start();

        return MatchExceptions(async () =>
        {
            var data = await CachedSearcher.Search(search, GetUserId() ?? 0);
            var result = services.mapper.Map<RequestResponse>(data);
            result.requestUser = GetUserId();
            sw.Stop();
            result.totalTime = sw.Elapsed.TotalMilliseconds;
            result.nonDbTime = result.totalTime - result.databaseTimes.Sum(x => x.Value);

            return result;
        });
    }

    [HttpGet("about")]
    public ActionResult<object> About()
    {
        return new {
            about = "The 'request' endpoint replaces the chainer and longpoller endpoints from the previous api. " +
                    "Because of the complexity of the request format, you now must POST to get results. " +
                    "You can request data from any 'table', and the query is in a kind of SQL format rather than " +
                    "search objects. You must name each request, because you can reference requests in later " +
                    "requests to perform 'chaining'. You can use values in your query, but they must be parameters " +
                    "in the form of @key, with the actual value put in the 'values' dictionary along with your request. " +
                    "Or, for simple scalar values, you can substitute a literal inside {{}}, so for instance, " +
                    "you can do 'id = {{123}}' or 'name = {{The Title}}'. " + 
                    "For examples, please see the unit tests in github: https://github.com/randomouscrap98/contentapi/blob/master/contentapi.test/Search/GenericSearchDbTests.cs",
            details = queryBuilder.GetAboutSearch()
        };
    }
}