using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers
{
    public class ModuleController : BaseViewServiceController<ModuleViewService, ModuleView, ModuleSearch>
    {
        protected IModuleService moduleService;
        protected IPermissionService permissionService;
        protected UnifiedModuleMessageViewService moduleMessageService;

        public ModuleController(BaseSimpleControllerServices services, ModuleViewService service, UnifiedModuleMessageViewService moduleMessageService,
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

        /// <summary>
        /// Modules can log debug information, useful for... well, debugging. Only supers can read these logs though!
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [Authorize]
        [HttpGet("debug/{name}")]
        public Task<ActionResult<List<string>>> GetDebug([FromRoute]string name)
        {
            return ThrowToAction(() =>
            {
                var requester = GetRequesterNoFail();
                if(!permissionService.IsSuper(requester))
                    throw new ForbiddenException("Can't read debug information unless super!");
                var modData = moduleService.GetModule(name);
                if(modData == null)
                    throw new NotFoundException($"No module with name {name}");
                return Task.FromResult(modData.debug.ToList());
            });
        }

        //[HttpGet("help")]
        //public Task<ActionResult<Dictionary<string, Dictionary<string, ModuleSubcommandInfo>>>> GetHelp()
        //{

        //}

        /// <summary>
        /// Allows you to POST either a new or updated module. The module service determines whether you have permission or not
        /// </summary>
        /// <param name="name"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("byname")]
        public Task<ActionResult<ModuleView>> PostByNameAsync([FromBody]ModuleView module)
        {
            return ThrowToAction(async () =>
            {
                //Go find by name first
                var existing = await service.FindByNameAsync(module.name);

                if(existing != null)
                    module.id = existing.Entity.id;
                else
                    module.id = 0;
                
                return await service.WriteAsync(module, GetRequesterNoFail());
            });
        }

        /// <summary>
        /// POST command data to a module. The arguments need not be parsed; just the full argument list as given (including subcommand)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="command"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("{name}")]
        public Task<ActionResult<string>> RunCommand([FromRoute]string name, [FromBody]string arguments)
        {
            return ThrowToAction(async () =>
            {
                var requester = GetRequesterNoFail();
                string result = null;
                //RunCommand should be thread safe, so just... run it async!
                await Task.Run(() => result = moduleService.RunCommand(name, arguments, requester));
                return result;
            });
        }

        /// <summary>
        /// Without polling, search through modules messages to get the ones you want. Users will generally not call this.
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        [Authorize]
        [HttpGet("messages")]
        public Task<ActionResult<List<UnifiedModuleMessageView>>> GetMessagesAsync([FromQuery]ModuleMessageViewSearch search)
        {
            return ThrowToAction(() =>
            {
                return moduleMessageService.SearchAsync(search, GetRequesterNoFail());
            });
        }
    }
}