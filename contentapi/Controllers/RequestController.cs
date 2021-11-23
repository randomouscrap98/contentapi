using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class RequestResponse
{
    public SearchRequests search {get;set;} = new SearchRequests();
    public Dictionary<string, object> data {get;set;} = new Dictionary<string, object>();
}

[ApiController]
[Route("[controller]")]
public class RequestController : BaseController
{
    protected IGenericSearch searcher;

    public RequestController(ILogger<RequestController> logger, IGenericSearch search) : base(logger)
    {
        this.searcher = search;
    }

    [HttpPost()]
    public Task<ActionResult<RequestResponse>> RequestAsync([FromBody]SearchRequests search)
    {
        return MatchExceptions(async () =>
        {
            return new RequestResponse()
            {
                search = search,
                data = await searcher.Search(search)
            };
        });
    }
}