using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    //public class ControllerServices
    //{
    //    public IEntityProvider provider;
    //    public IMapper mapper;
    //    public Keys keys;
    //    public SystemConfig systemConfig;
    //    public IPermissionService permissions;
    //    public IActivityService activity;
    //    public IHistoryService history;

    //    public ControllerServices(IEntityProvider provider, IMapper mapper, Keys keys, SystemConfig systemConfig, 
    //        IPermissionService permissions, IActivityService activityService, IHistoryService history)
    //    {
    //        this.provider = provider;
    //        this.mapper = mapper;
    //        this.keys = keys;
    //        this.systemConfig = systemConfig;
    //        this.permissions = permissions;
    //        this.activity = activityService;
    //        this.history = history;
    //    }
    //}

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
        //protected ControllerServices services;

        protected Keys keys;
        protected ILogger logger;

        public BaseSimpleController(Keys keys, ILogger<BaseSimpleController> logger)
        {
            this.keys = keys;
            this.logger = logger;
        }

        protected long GetRequesterUid()
        {
            //Look for the UID from the JWT 
            var id = User.FindFirstValue(keys.UserIdentifier);

            if(id == null)
                throw new InvalidOperationException("User not logged in!");
            
            return long.Parse(id);
        }

        protected long GetRequesterUidNoFail()
        {
            try { return GetRequesterUid(); }
            catch { return -1; }
        }

        protected ViewRequester GetRequesterNoFail()
        {
            return new ViewRequester() { userId = GetRequesterUidNoFail() };
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