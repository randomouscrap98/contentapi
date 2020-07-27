using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class ModuleController : BaseViewServiceController<ModuleViewService, ModuleView, ModuleSearch>
    {
        protected IModuleService moduleService;
        protected IPermissionService permissionService;
        protected ModuleMessageViewService moduleMessageService;

        public ModuleController(BaseSimpleControllerServices services, ModuleViewService service, ModuleMessageViewService moduleMessageService,
            IPermissionService permissionService, IModuleService moduleService)//UserViewService service, IPermissionService permissionService) 
            : base(services, service) 
        {
            this.moduleMessageService = moduleMessageService;
            this.permissionService = permissionService;
            this.moduleService = moduleService;
        }

        protected override async Task SetupAsync()
        {
            await service.SetupAsync();
        }

        [Authorize]
        [HttpGet("debug/{name}")]
        public Task<ActionResult<List<string>>> GetDebug([FromRoute]string name)
        {
            return ThrowToAction(() =>
            {
                var requester = GetRequesterNoFail();
                if(!permissionService.IsSuper(requester))
                    throw new AuthorizationException("Can't read debug information unless super!");
                var modData = moduleService.GetModule(name);
                if(modData == null)
                    throw new NotFoundException($"No module with name {name}");
                return Task.FromResult(modData.debug.ToList());
            });
        }

        [Authorize]
        [HttpPost("{name}")]
        public Task<ActionResult<ModuleView>> PostByNameAsync([FromRoute]string name, [FromBody]ModuleView module)
        {
            return ThrowToAction(async () =>
            {
                //Go find by name first
                var existing = await service.FindByNameAsync(name);

                if(existing != null)
                    module.id = existing.Entity.id;
                else
                    module.id = 0;
                
                return await service.WriteAsync(module, GetRequesterNoFail());
            });
        }

        [Authorize]
        [HttpPost("{name}/{command}")]
        public Task<ActionResult<string>> RunCommand([FromRoute]string name, [FromRoute]string command, [FromBody]string data)
        {
            return ThrowToAction(async () =>
            {
                var requester = GetRequesterNoFail();
                string result = null;
                //RunCommand should be thread safe, so just... run it async!
                await Task.Run(() => result = moduleService.RunCommand(name, command, data, requester));
                return result;
            });
        }

        [Authorize]
        [HttpGet("messages")]
        public Task<ActionResult<List<ModuleMessageView>>> GetMessagesAsync([FromQuery]ModuleMessageViewSearch search)
        {
            return ThrowToAction(() =>
            {
                return moduleMessageService.SearchAsync(search, GetRequesterNoFail());
            });
        }
    }
}