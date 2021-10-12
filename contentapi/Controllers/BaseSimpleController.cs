using System;
using System.Security.Claims;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class BaseSimpleControllerServices //where T : BaseSimpleController
    {
        public ILogger<BaseSimpleController> logger;
        public UserValidationService userValidation;

        public BaseSimpleControllerServices(ILogger<BaseSimpleController> logger, UserValidationService userValidation)
        {
            this.logger = logger;
            this.userValidation = userValidation;
        }
    }

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
        protected UserValidationService userValidation;
        protected BaseSimpleControllerServices services;

        public BaseSimpleController(BaseSimpleControllerServices services) //ILogger<BaseSimpleController> logger, UserValidationService userValidation)
        {
            this.logger = services.logger;
            this.userValidation = services.userValidation;
            this.services = services;
        }

        protected virtual Task SetupAsync() { return Task.CompletedTask; } 

        protected long GetRequesterUid()
        {
            //Look for the UID from the JWT 
            var id = User.FindFirstValue(Keys.UserIdentifier);
            var token = User.FindFirstValue(Keys.UserValidate);

            if(id == null)
                throw new InvalidOperationException("User not logged in!");

            var user = long.Parse(id);

            //ALL of the AFTER MIDDLEWARE token checks (whatever you add) NEED to throw the SAME kinds of errors as 
            //an actual invalid token ie bad formatting or missing or whatever
            if (token == null || token != userValidation.GetUserValidationToken(user))
                throw new UnauthorizedAccessException("User not logged in!");
            
            return user;
        }

        protected long GetRequesterUidNoFail()
        {
            try { return GetRequesterUid(); }
            catch(InvalidOperationException) { return -1; }
        }

        protected Requester GetRequesterNoFail()
        {
            return new Requester() { userId = GetRequesterUidNoFail() };
        }

        protected async Task<ActionResult<T>> ThrowToAction<T>(Func<Task<T>> action)
        {
            try
            {
                try
                {
                    await SetupAsync();

                    //Go find the parent. If it's not content, BAD BAD BAD
                    return await action();
                }
                catch (AggregateException ex)
                {
                    throw ex.Flatten().InnerException;
                }
            }
            //catch(AuthorizationException ex)
            //{
            //    return Unauthorized(ex.Message);
            //}
            //I keep using this on accident
            catch(UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch(ForbiddenException ex)
            {
                return StatusCode(403, ex.Message); //Forbid(ex.Message);
            }
            catch(BannedException ex)
            {
                return StatusCode(418, ex.Message); //new StatusCodeResult()
            }
            catch(BadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(NotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch(TimeoutException)
            {
                Response.Headers.Add("SBS-Warning", "Non-critical: timeout");
                return NoContent();
                //return null;
                //return StatusCode(408);
            }
            catch(OperationCanceledException)
            {
                Response.Headers.Add("SBS-Warning", "Non-critical: operation cancelled");
                logger.LogWarning("Pageload(?) got cancelled");
                return NoContent();
            }
        }

    }
}