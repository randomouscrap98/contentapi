using contentapi.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using contentapi.Services.Extensions;
using contentapi.Services.Constants;

namespace contentapi.Services.Implementations
{
    public class ModuleViewService : BaseEntityViewService<ModuleView, ModuleSearch>
    {
        protected ModuleService service;

        public ModuleViewService(ILogger<ModuleViewService> logger, ViewServicePack services, ModuleViewSource converter,
            ModuleService service) :base(services, logger, converter) 
        { 
            this.service = service;
        }

        public override string EntityType => Keys.ModuleType;

        public override async Task<ModuleView> CleanViewGeneralAsync(ModuleView view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);

            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Only supers can create modules!");

            var found = await FindByNameAsync(view.name);

            if(found != null && found.Entity.id != view.id)
                throw new BadRequestException($"A module with name '{view.name}' already exists!");

            return view;
        }

        public override async Task<ModuleView> WriteAsync(ModuleView view, Requester requester)
        {
            var result = await base.WriteAsync(view, requester);
            service.UpdateModule(result);
            return result;
        }

        public override async Task<ModuleView> DeleteAsync(long entityId, Requester requester)
        {
            var result = await base.DeleteAsync(entityId, requester);
            service.RemoveModule(result.name);
            return result;
        }

        public override Task<List<ModuleView>> PreparedSearchAsync(ModuleSearch search, Requester requester)
        {
            //NO permissions check! All modules are readable!
            return converter.SimpleSearchAsync(search);
        }
    }
}