using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class CommentController : BaseDeletableController<CommentView> 
    {
        protected CommentViewService service;

        public CommentController(BaseSimpleControllerServices services, CommentViewService service) 
            : base(services)
        //public CommentController(ILogger<CommentController> logger, CommentViewService service) 
        //    : base(logger)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<List<CommentView>>> GetAsync([FromQuery]CommentSearch search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpGet("aggregate")]
        public Task<ActionResult<List<CommentAggregateView>>> GetAggregateAsync([FromQuery]CommentSearch search)
        {
            return ThrowToAction(() => service.SearchAggregateAsync(search, GetRequesterNoFail()));
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

        //[HttpPost("rethread")]
        //[Authorize]
        //public Task<ActionResult<List<CommentView>>> PutRethreadAsync([FromBody]CommentRethread rethread)
        //{
        //    return ThrowToAction<CommentView>(() =>
        //    {
        //        view.id = id;
        //        return service.WriteAsync(view, GetRequesterNoFail());
        //    });
        //}

        protected override Task<ActionResult<CommentView>> DeleteAsync([FromRoute] long id)
        {
            return ThrowToAction<CommentView>(() => service.DeleteAsync(id, GetRequesterNoFail()));
        }
    }
}