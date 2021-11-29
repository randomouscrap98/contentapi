using System.Diagnostics;
using contentapi.Search;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class RequestResponse
{
    public SearchRequests search {get;set;} = new SearchRequests();
    public Dictionary<string, IEnumerable<IDictionary<string, object>>> data {get;set;} = 
        new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
    public double time {get;set;}
    public bool loggedIn {get;set;}
}

public class RequestController : BaseController
{
    protected IGenericSearch searcher;
    protected IQueryBuilder queryBuilder;

    public RequestController(BaseControllerServices services, IGenericSearch search,
        IQueryBuilder queryBuilder) : base(services)
    {
        this.searcher = search;
        this.queryBuilder = queryBuilder;
    }

    [HttpPost()]
    public Task<ActionResult<RequestResponse>> RequestAsync([FromBody]SearchRequests search)
    {
        return MatchExceptions(async () =>
        {
            var sw = new Stopwatch();

            sw.Start();
            var data = await searcher.SearchRestricted(search);
            sw.Stop();

            return new RequestResponse()
            {
                search = search,
                data = data,
                time = sw.Elapsed.TotalMilliseconds,
                loggedIn = IsUserLoggedIn()
            };
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
                    "in the form of @value. For examples, please see the unit tests in github: https://github.com/randomouscrap98/contentapi/blob/master/contentapi.test/Search/GenericSearchDbTests.cs",
            details = queryBuilder.GetAboutSearch()
        };
    }
}