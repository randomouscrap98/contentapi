using contentapi.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using contentapi.Services.Extensions;
using contentapi.Services.Constants;
using System.Text.RegularExpressions;
using Randomous.EntitySystem;
using System.Text.Json;

namespace contentapi.Services.Implementations
{
    /// <summary>
    /// The combined module view and loaded module service.
    /// </summary>
    /// <remarks>
    /// This is another 
    /// </remarks>
    public class ModuleViewService : BaseEntityViewService<ModuleView, ModuleSearch>
    {
        protected ModuleServiceConfig config;
        protected ModuleMessageViewService moduleMessageService;
        protected IModuleService moduleService;
        protected CacheService<string, List<ModuleView>> cache;

        public ModuleViewService(ILogger<ModuleViewService> logger, ViewServicePack services, ModuleViewSource converter,
            ModuleServiceConfig config, ModuleMessageViewService moduleMessageService, IModuleService service,
            CacheService<string, List<ModuleView>> cache) :base(services, logger, converter) 
        { 
            this.config = config;
            this.moduleMessageService = moduleMessageService;
            this.moduleService = service;
            this.cache = cache;
        }

        public override string EntityType => Keys.ModuleType;

        // -- View stuff --

        /// <summary>
        /// Need all the cached data/etc set up before we can serve up views! We store all modules in memory
        /// </summary>
        /// <returns></returns>
        public async Task SetupAsync()
        {
            var modules = await SearchAsync(new ModuleSearch(), new Requester() { system = true });
            foreach(var module in modules)
                moduleService.UpdateModule(module, false);
        }

        /// <summary>
        /// This is a write/permission check performed whether updating OR inserting
        /// </summary>
        /// <param name="view"></param>
        /// <param name="requester"></param>
        /// <returns></returns>
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
            cache.PurgeCache();
            var result = await base.WriteAsync(view, requester);
            moduleService.UpdateModule(result);
            return result;
        }

        public override async Task<ModuleView> DeleteAsync(long entityId, Requester requester)
        {
            cache.PurgeCache();
            var result = await base.DeleteAsync(entityId, requester);
            moduleService.RemoveModule(result.name);
            return result;
        }

        public override async Task<List<ModuleView>> PreparedSearchAsync(ModuleSearch search, Requester requester)
        {
            string key = JsonSerializer.Serialize(search) + JsonSerializer.Serialize(requester); 
            List<ModuleView> baseResult = null;

            if(!cache.GetValue(key, ref baseResult))
            {
                //NO permissions check! All modules are readable!
                baseResult = await converter.SimpleSearchAsync(search);
                baseResult.ForEach(x => x.subcommands = moduleService.ParseAllSubcommands(moduleService.GetModule(x.name)));
                cache.StoreItem(key, baseResult);
            }

            return baseResult;
        }
    }
}