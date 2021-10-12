using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    public class UnifiedModuleMessageViewServiceProfile : Profile
    {
        public UnifiedModuleMessageViewServiceProfile()
        {
            CreateMap<ModuleMessageView, UnifiedModuleMessageView>().ReverseMap();
            //CreateMap<A, C>()
            //    .ConvertUsing((entity, c, context) =>
            //        {
            //        var intermediate = context.Mapper.Map<B>(entity);
            //        return context.Mapper.Map<C>(intermediate);
            //        });
        }
    }

    public class UnifiedModuleMessageViewService : /*BaseViewServices<ModuleMessageView, ModuleMessageViewSearch>,*/ IViewReadService<UnifiedModuleMessageView, ModuleMessageViewSearch>
    {
        protected ILogger logger;
        protected ViewServicePack services;
        protected ModuleMessageViewSource moduleMessageSource;
        protected ModuleMessageViewService moduleMessageService;
        protected ModuleRoomMessageViewService moduleRoomMessageService;

        public UnifiedModuleMessageViewService(ViewServicePack services, ILogger<UnifiedModuleMessageViewService> logger, 
            ModuleMessageViewSource moduleMessageSource, ModuleMessageViewService moduleMessageService,
            ModuleRoomMessageViewService moduleRoomMessageService)
            //: base(services, logger) 
        { 
            this.logger = logger;
            this.services = services;
            this.moduleMessageSource = moduleMessageSource;
            this.moduleMessageService = moduleMessageService;
            this.moduleRoomMessageService = moduleRoomMessageService;
        }

        //A special endpoint for MODULES (not users) to add module messages
        public async Task<UnifiedModuleMessageView> AddMessageAsync(UnifiedModuleMessageView basic) //long senderuid, long receiveruid, string message, string module)
        {
            //This is IF the message is 
            return services.mapper.Map<UnifiedModuleMessageView>(await moduleMessageService.AddMessageAsync(basic));
        }

        public async Task<List<UnifiedModuleMessageView>> SearchAsync(ModuleMessageViewSearch search, Requester requester)
        {
            var result = await moduleMessageService.SearchAsync(search, requester);
            return result.Select(x => services.mapper.Map<UnifiedModuleMessageView>(x)).ToList();
        }
    }
}