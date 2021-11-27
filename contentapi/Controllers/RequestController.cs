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

[ApiController]
[Route("[controller]")]
public class RequestController : BaseController
{
    protected IGenericSearch searcher;

    public RequestController(BaseControllerServices services, IGenericSearch search) : base(services)
    {
        this.searcher = search;
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
}