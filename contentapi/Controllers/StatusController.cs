using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController : BaseController
{
    protected IWebHostEnvironment environment;
    protected IRuntimeInformation runtimeInformation;


    public StatusController(BaseControllerServices services, IWebHostEnvironment environment, IRuntimeInformation rinfo) 
        : base(services)
    {
        this.environment = environment;
        this.runtimeInformation = rinfo;
    }

    [HttpGet()]
    public object GetGeneralStatus()
    {
        return new {
            version = GetType().Assembly.GetName().Version?.ToString(),
            appname = environment.ApplicationName,
            environment = environment.EnvironmentName,
            processStart = runtimeInformation.ProcessStart,
            runtime = runtimeInformation.ProcessRuntime
        };
    }

    [HttpGet("token")]
    [Authorize]
    public object GetAboutToken()
    {
        return new {
            userId = GetUserId()
        };
    }

}