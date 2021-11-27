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
        catch(ArgumentException ex)
        {
            return BadRequest($"Argument error: {ex.Message}");
        }
    }
}