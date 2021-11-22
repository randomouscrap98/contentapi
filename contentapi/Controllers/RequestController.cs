using Microsoft.AspNetCore.Mvc;

namespace contentapi;

[ApiController]
[Route("[controller]")]
public class RequestController
{
    protected ILogger logger;


    public RequestController(ILogger<RequestController> logger)
    {
        this.logger = logger;
    }

    [HttpPost()]
    public async Task<object> RequestAsync([FromBody]SearchRequests search)
    {
        return new {
            count = search.requests.Count,
            search = search
        };
    }
}