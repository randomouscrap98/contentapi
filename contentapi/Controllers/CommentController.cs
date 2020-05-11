using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class CommentController : BaseSimpleController
    {
        protected CommentViewService service;

        public CommentController(ILogger<BaseSimpleController> logger, CommentViewService service) 
            : base(logger)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<IList<CommentView>>> GetAsync([FromQuery]CommentSearch search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpGet("listen/{parentId}/listeners")]
        public Task<ActionResult<List<CommentListener>>> GetListenersAsync([FromRoute]long parentId, [FromQuery]List<long> lastListeners, CancellationToken token)
        {
            return ThrowToAction(() => service.GetListenersAsync(parentId, lastListeners, GetRequesterNoFail(), token));
        }

        [HttpGet("listen/{parentId}")]
        [Authorize]
        public Task<ActionResult<List<CommentView>>> ListenAsync([FromRoute]long parentId, [FromQuery]long lastId, [FromQuery]long firstId, CancellationToken token)
        {
            return ThrowToAction(() => service.ListenAsync(parentId, lastId, firstId, GetRequesterNoFail(), token));
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<CommentView>> PostAsync([FromBody]CommentView view)
        {
            return ThrowToAction<CommentView>(() => 
            {
                view.id = 0;
                return service.WriteAsync(view, GetRequesterNoFail());
            });
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<CommentView>> PutAsync([FromRoute]long id, [FromBody]CommentView view)
        {
            return ThrowToAction<CommentView>(() =>
            {
                view.id = id;
                return service.WriteAsync(view, GetRequesterNoFail());
            });
        }

        [HttpDelete("{id}")]
        [Authorize]
        public Task<ActionResult<CommentView>> DeleteAsync([FromRoute] long id)
        {
            return ThrowToAction<CommentView>(() => service.DeleteAsync(id, GetRequesterNoFail()));
        }
    }
}