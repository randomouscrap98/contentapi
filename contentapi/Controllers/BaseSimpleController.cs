using System;
using System.Security.Claims;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    /// <summary>
    /// A bunch of methods extending the existing IProvider
    /// </summary>
    /// <remarks>
    /// Even though this extends from controller, it SHOULD NOT EVER use controller functions
    /// or fields or any of that. This is just a little silliness, I'm slapping stuff together.
    /// This is still testable without it being a controller though: please test sometime.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public abstract class BaseSimpleController : ControllerBase
    {
        protected ILogger logger;

        public BaseSimpleController(ILogger<BaseSimpleController> logger)
        {
            this.logger = logger;
        }

        protected long GetRequesterUid()
        {
            //Look for the UID from the JWT 
            var id = User.FindFirstValue(Keys.UserIdentifier);

            if(id == null)
                throw new InvalidOperationException("User not logged in!");
            
            return long.Parse(id);
        }

        protected long GetRequesterUidNoFail()
        {
            try { return GetRequesterUid(); }
            catch { return -1; }
        }

        protected Requester GetRequesterNoFail()
        {
            return new Requester() { userId = GetRequesterUidNoFail() };
        }

        protected async Task<ActionResult<T>> ThrowToAction<T>(Func<Task<T>> action)
        {
            try
            {
                //Go find the parent. If it's not content, BAD BAD BAD
                return await action();
            }
            catch(AuthorizationException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch(BadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(TimeoutException)
            {
                Response.Headers.Add("SBS-Warning", "Non-critical timeout");
                return null;
                //return StatusCode(408);
            }
            catch(OperationCanceledException)
            {
                logger.LogWarning("Pageload(?) got cancelled");
                return NoContent();
            }
        }

    }
}