using contentapi.Security;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class BaseControllerServices
{
    public ILogger<BaseController> logger;
    public IAuthTokenService<long> authService;

    public BaseControllerServices(ILogger<BaseController> logger, IAuthTokenService<long> authService)
    {
        this.logger = logger;
        this.authService = authService;
    }
}

[ApiController]
[Route("contentapi/[controller]")]
public class BaseController : Controller
{
    protected BaseControllerServices services;

    public BaseController(BaseControllerServices services)
    {
        this.services = services;
    }

    protected long? GetUserId() => services.authService.GetUserId(User.Claims);
    protected bool IsUserLoggedIn() => GetUserId() != null;

    protected async Task<ActionResult<T>> MatchExceptions<T>(Func<Task<T>> perform)
    {
        try
        {
            return await perform();
        }
        catch(Exception ex)
        {
            if(ex is AggregateException)
                ex = ex.InnerException ?? throw new InvalidOperationException("Aggregate exception did not have inner exception!", ex); //Grab the first inner exception

            if(ex is ArgumentException)
                return BadRequest($"Argument error: {ex.Message}");
            
            //Just rethrow if we couldn't figure out what it was.
            throw;
        }
    }
}