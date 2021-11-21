using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController
{
    protected ILogger logger;

    public StatusController(ILogger<StatusController> logger)
    {
        this.logger = logger;
    }

    [HttpGet()]
    public async Task<object> GetGeneralStatusAsync()
    {
        return new {
            version = GetType().Assembly.GetName().Version?.ToString(),
            //processStart = StaticRuntime.ProcessStart,
            //runtime = DateTime.Now - StaticRuntime.ProcessStart 
        };
    }

}