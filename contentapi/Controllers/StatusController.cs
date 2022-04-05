using contentapi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class StatusControllerConfig 
{
    public string Repo {get;set;} = "";
    public string BugReports {get;set;} = "";
    public string Contact {get;set;} = "";
}

public class StatusController : BaseController
{
    protected IWebHostEnvironment environment;
    protected IRuntimeInformation runtimeInformation;
    protected StatusControllerConfig config;


    public StatusController(BaseControllerServices services, IWebHostEnvironment environment, IRuntimeInformation rinfo, StatusControllerConfig config) 
        : base(services)
    {
        this.environment = environment;
        this.runtimeInformation = rinfo;
        this.config = config;
    }

    [HttpGet()]
    public object GetGeneralStatus()
    {
        return new {
            version = GetType().Assembly.GetName().Version?.ToString(),
            appname = environment.ApplicationName,
            environment = environment.EnvironmentName,
            processStart = runtimeInformation.ProcessStart,
            runtime = runtimeInformation.ProcessRuntime,
            repo = config.Repo,
            bugreport = config.BugReports,
            contact = config.Contact
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