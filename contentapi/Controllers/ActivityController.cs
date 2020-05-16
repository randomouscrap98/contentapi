using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class ActivityController : BaseSimpleController
    {
        protected ActivityViewService service;

        public ActivityController(ILogger<BaseSimpleController> logger,
            ActivityViewService service) : base(logger)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<ActivityResultView>> GetActivityAsync([FromQuery]CombinedActivitySearch search)
        {
            return ThrowToAction(() => service.SearchResultAsync(search, GetRequesterNoFail()));
        }
    }
}