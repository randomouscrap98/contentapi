using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    [Authorize]
    public class VoteController : BaseDeletableController<VoteView>
    {
        protected VoteViewService service;

        public VoteController(BaseSimpleControllerServices services, VoteViewService service) : base(services)
        {
            this.service = service;
        }

        protected override Task SetupAsync() { return service.SetupAsync(); }

        protected override Task<ActionResult<VoteView>> DeleteAsync(long id)
        {
            var requester = GetRequesterNoFail();

            return ThrowToAction(async () =>
            {
                return await service.DeleteAsync((await service.GetByContentId(id, requester)).id, requester);
            });
        }

        [HttpGet]
        public Task<ActionResult<List<VoteView>>> GetVotesAsync([FromQuery]VoteSearch search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPost("{id}/{vote}")]
        public Task<ActionResult<VoteView>> PostVote([FromRoute]long id, [FromRoute]string vote)
        {
            var requester = GetRequesterNoFail();
            return ThrowToAction(() => service.WriteAsync(new VoteView() { userId = requester.userId, contentId = id, vote = vote }, requester));
        }
    }
}