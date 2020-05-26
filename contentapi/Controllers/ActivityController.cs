using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
        public Task<ActionResult<List<ActivityView>>> GetActivityAsync([FromQuery]ActivitySearch search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpGet("aggregate")]
        public Task<ActionResult<List<ActivityAggregateView>>> GetAggregateAsync([FromQuery]ActivitySearch search)
        {
            return ThrowToAction(() => service.SearchAggregateAsync(search, GetRequesterNoFail()));
        }
    }
}