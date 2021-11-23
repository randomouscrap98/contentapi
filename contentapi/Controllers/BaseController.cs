using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

[ApiController]
[Route("[controller]")]
public class BaseController : Controller
{
    protected ILogger logger;

    public BaseController(ILogger logger)
    {
        this.logger = logger;
    }

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