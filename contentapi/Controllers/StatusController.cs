using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController
{
    protected ILogger logger;
    protected IWebHostEnvironment environment;
    protected IRuntimeInformation runtimeInformation;


    public StatusController(ILogger<StatusController> logger, IWebHostEnvironment environment, IRuntimeInformation rinfo)
    {
        this.logger = logger;
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

}