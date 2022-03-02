using AutoMapper;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class BaseControllerServices
{
    public ILogger<BaseController> logger;
    public IMapper mapper;
    public IAuthTokenService<long> authService;

    public BaseControllerServices(ILogger<BaseController> logger, IAuthTokenService<long> authService, 
        IMapper mapper)
    {
        this.logger = logger;
        this.authService = authService;
        this.mapper = mapper;
    }
}

[ApiController]
[Route("api/[controller]")]
public class BaseController : Controller
{
    protected BaseControllerServices services;

    public BaseController(BaseControllerServices services)
    {
        this.services = services;
    }

    protected long? GetUserId() => services.authService.GetUserId(User.Claims);
    protected bool IsUserLoggedIn() => GetUserId() != null;
    protected long GetUserIdStrict() => services.authService.GetUserId(User.Claims) ?? throw new InvalidOperationException("User not logged in! Strict mode on: MUST be logged in for this call!");

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

            if(ex is ArgumentException || ex is RequestException || ex is ParseException)
                return BadRequest($"Request error: {ex.Message}");

            if(ex is NotFoundException)
                return NotFound($"Not found: {ex.Message}");

            if(ex is ForbiddenException)
                return new ObjectResult($"Forbidden error: {ex.Message}") { StatusCode = 403 }; //Forbidden
            
            //Just rethrow if we couldn't figure out what it was.
            throw;
        }
    }
}