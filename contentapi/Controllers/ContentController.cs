using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class ContentController : BaseViewServiceController<ContentViewService, ContentView, ContentSearch>
    {
        public ContentController(ILogger<BaseSimpleController> logger, ContentViewService service) 
            : base(logger, service) { }
        
        protected override Task SetupAsync()
        {
            return service.SetupAsync();
        }

        protected Task<ActionResult<ContentView>> Vote(long id, ContentVote vote)
        {
            return ThrowToAction(async () =>
            {
                await SetupAsync();
                var requester = GetRequesterNoFail();
                await service.Vote(id, vote, requester);
                return await service.FindByIdAsync(id, requester);
            });
        }

        [HttpPost("{id}/vote/up")]
        [Authorize]
        public Task<ActionResult<ContentView>> Upvote([FromRoute]long id) { return Vote(id, ContentVote.Up); }

        [HttpPost("{id}/vote/down")]
        [Authorize]
        public Task<ActionResult<ContentView>> Downvote([FromRoute]long id) { return Vote(id, ContentVote.Down); }

        [HttpDelete("{id}/vote")]
        [Authorize]
        public Task<ActionResult<ContentView>> DeleteVote([FromRoute]long id) { return Vote(id, ContentVote.None); }
    }
}