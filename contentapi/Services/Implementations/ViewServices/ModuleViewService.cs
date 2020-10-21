using contentapi.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using contentapi.Services.Extensions;
using contentapi.Services.Constants;
using System.Text.RegularExpressions;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    /// <summary>
    /// The combined module view and loaded module service.
    /// </summary>
    public class ModuleViewService : BaseEntityViewService<ModuleView, ModuleSearch>
    {
        protected ModuleServiceConfig config;
        protected ModuleMessageViewService moduleMessageService;
        protected IModuleService moduleService;

        public ModuleViewService(ILogger<ModuleViewService> logger, ViewServicePack services, ModuleViewSource converter,
            ModuleServiceConfig config, ModuleMessageViewService moduleMessageService, IModuleService service) :base(services, logger, converter) 
        { 
            this.config = config;
            this.moduleMessageService = moduleMessageService;
            this.moduleService = service;
        }

        public override string EntityType => Keys.ModuleType;

        // -- View stuff --

        public async Task SetupAsync()
        {
            var modules = await SearchAsync(new ModuleSearch(), new Requester() { system = true });
            foreach(var module in modules)
                moduleService.UpdateModule(module, false);
        }

        public override async Task<ModuleView> CleanViewGeneralAsync(ModuleView view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);

            FailUnlessSuper(requester);

            if(!Regex.IsMatch(view.name, "^[a-z0-9_]+$"))
                throw new BadRequestException("Module name can only be lowercase letters, numbers, and _");

            var found = await FindByNameAsync(view.name);

            if(found != null && found.Entity.id != view.id)
                throw new BadRequestException($"A module with name '{view.name}' already exists!");

            return view;
        }

        public override async Task<EntityPackage> DeleteCheckAsync(long entityId, Requester requester) 
        {
            var result = await base.DeleteCheckAsync(entityId, requester);
            FailUnlessSuper(requester);
            return result;
        }

        public override async Task<ModuleView> WriteAsync(ModuleView view, Requester requester)
        {
            var result = await base.WriteAsync(view, requester);
            moduleService.UpdateModule(result);
            return result;
        }

        public override async Task<ModuleView> DeleteAsync(long entityId, Requester requester)
        {
            var result = await base.DeleteAsync(entityId, requester);
            moduleService.RemoveModule(result.name);
            return result;
        }

        public override Task<List<ModuleView>> PreparedSearchAsync(ModuleSearch search, Requester requester)
        {
            //NO permissions check! All modules are readable!
            return converter.SimpleSearchAsync(search);
        }

        // -- Loaded module stuff --

    }
}