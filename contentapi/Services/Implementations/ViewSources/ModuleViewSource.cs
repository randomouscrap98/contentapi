using System.Collections.Generic;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class ModuleSearch : BaseHistorySearch
    {
        public string NameLike {get;set;}
        public List<string> Names {get;set;} = new List<string>();
    }

    public class ModuleViewSourceProfile : Profile
    {
        public ModuleViewSourceProfile()
        {
            CreateMap<ModuleSearch, EntitySearch>();
        }
    }

    public class ModuleViewSource : BaseEntityViewSource<ModuleView, EntityGroup, ModuleSearch>
    {
        public override string EntityType => Keys.ModuleType;

        public ModuleViewSource(ILogger<ModuleViewSource> logger, BaseViewSourceServices services)
            : base(logger, services) {}

        public override ModuleView ToView(EntityPackage module)
        {
            var result = new ModuleView() 
            { 
                name = module.Entity.name, 
                code = module.Entity.content
            };

            if(module.HasValue(Keys.ModuleDescriptionKey))
                result.description = module.GetValue(Keys.ModuleDescriptionKey).value;

            ApplyToEditView(module, result);
            ApplyToValueView(module, result);

            return result;
        }

        public override EntityPackage FromView(ModuleView module)
        {
            var package = this.NewEntity(module.name, module.code)
                .Add(NewValue(Keys.ModuleDescriptionKey, module.description));
            ApplyFromEditView(module, package, EntityType);
            ApplyFromValueView(module, package, EntityType);
            return package;
        }
    }
}