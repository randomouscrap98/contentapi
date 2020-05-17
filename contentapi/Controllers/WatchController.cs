using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    [Authorize]
    public class WatchController : BaseSimpleController
    {
        protected WatchViewService service;

        public WatchController(ILogger<BaseSimpleController> logger,
            WatchViewService service) : base(logger)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<List<WatchView>>> GetWatchesAsync([FromQuery]WatchSearch search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPost("{id}")]
        public Task<ActionResult<WatchView>> PostWatch([FromRoute]long id)
        {
            var view = new WatchView() { contentId = id };
            return ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail())); //service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPut("{id}/{newLast}")]
        public Task<ActionResult<WatchView>> PutWatch([FromRoute]long id, [FromRoute]long newLast)
        {
            var requester = GetRequesterNoFail();

            return ThrowToAction(async () => 
            {
                var view = await service.GetByContentId(id, requester);
                view.lastNotificationId = newLast;
                return await service.WriteAsync(view, requester);
            });
        }

        [HttpDelete("{id}")]
        public Task<ActionResult<WatchView>> DeleteWatch([FromRoute]long id)
        {
            var requester = GetRequesterNoFail();

            return ThrowToAction(async () =>
            {
                return await service.DeleteAsync((await service.GetByContentId(id, requester)).id, requester);
            });
        }
    }
}