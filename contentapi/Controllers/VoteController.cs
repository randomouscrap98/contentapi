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
    public class VoteController : BaseSimpleController
    {
        protected VoteViewService service;

        public VoteController(ILogger<BaseSimpleController> logger,
            VoteViewService service) : base(logger)
        {
            this.service = service;
        }

        protected override Task SetupAsync() { return service.SetupAsync(); }

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

        [HttpDelete("{id}")]
        public Task<ActionResult<VoteView>> DeleteVote([FromRoute]long id)
        {
            var requester = GetRequesterNoFail();

            return ThrowToAction(async () =>
            {
                return await service.DeleteAsync((await service.GetByContentId(id, requester)).id, requester);
            });
        }
    }
}