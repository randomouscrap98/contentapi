using contentapi.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using AutoMapper;
using contentapi.Services.Extensions;
using Randomous.EntitySystem.Extensions;
using contentapi.Services.Constants;

namespace contentapi.Services.Implementations
{
    public class ModuleViewService : BaseEntityViewService<ModuleView, ModuleSearch>
    {
        public ModuleViewService(ILogger<ModuleViewService> logger, ViewServicePack services, ModuleViewSource converter)
            :base(services, logger, converter) { }

        public override string EntityType => Keys.ModuleType;

        public override async Task<ModuleView> CleanViewGeneralAsync(ModuleView view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);
            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Only supers can create modules!");
            return view;
        }

        public override Task<List<ModuleView>> PreparedSearchAsync(ModuleSearch search, Requester requester)
        {
            //NO permissions check! All modules are readable!
            return converter.SimpleSearchAsync(search);
        }
    }
}