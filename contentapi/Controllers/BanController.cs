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
    public class BanController : BaseSimpleController
    {
        protected PublicBanViewService service;

        public BanController(BaseSimpleControllerServices services, PublicBanViewService service) : base(services)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<List<PublicBanView>>> GetActivityAsync([FromQuery]BanSearch search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPost()]
        public Task<ActionResult<PublicBanView>> PostAsync([FromBody]PublicBanView ban)
        {
            return ThrowToAction(() => service.WriteAsync(ban, GetRequesterNoFail()));
        }
    }
}