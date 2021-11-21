using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers
{
    [Authorize]
    public class BanController : BaseSimpleController
    {
        //protected PublicBanViewService service;
        protected BanViewService service;

        public BanController(BaseSimpleControllerServices services, BanViewService service) : base(services)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<List<BanView>>> GetActivityAsync([FromQuery]BanSearch search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPost()]
        public Task<ActionResult<BanView>> PostAsync([FromBody]BanView ban)
        {
            return ThrowToAction(() => service.WriteAsync(ban, GetRequesterNoFail()));
        }
    }
}