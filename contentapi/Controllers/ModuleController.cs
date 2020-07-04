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

        public ModuleController(ILogger<ModuleController> logger, ModuleViewService service, IModuleService moduleService)//UserViewService service, IPermissionService permissionService) 
            : base(logger, service) 
        {
            this.moduleService = moduleService;
        }

        protected override async Task SetupAsync()
        {
            await service.SetupAsync();
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
            return ThrowToAction(() =>
            {
                var requester = GetRequesterNoFail();
                var result = moduleService.RunCommand(name, command, data, requester);
                return Task.FromResult(result);
            });
        }
    }
}