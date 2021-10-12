using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    public class UnifiedModuleMessageViewSearch : ModuleMessageViewSearch
    {
        public List<long> ParentIds {get;set;} = new List<long>();
    }

    public class UnifiedModuleMessageViewServiceProfile : Profile
    {
        public UnifiedModuleMessageViewServiceProfile()
        {
            CreateMap<ModuleMessageView, UnifiedModuleMessageView>().ReverseMap();
            CreateMap<UnifiedModuleMessageViewSearch, ModuleMessageViewSearch>().ReverseMap();

            //Need maps for: 
            // unifiedmodulemessageviewsearch to commentsearch (and back?),
            // unifiedmodulemessageview to commentview (and back)

            //Probably don't need this transitive mapping:
            //CreateMap<A, C>()
            //    .ConvertUsing((entity, c, context) =>
            //        {
            //        var intermediate = context.Mapper.Map<B>(entity);
            //        return context.Mapper.Map<C>(intermediate);
            //        });
        }
    }

    public class UnifiedModuleMessageViewService : /*BaseViewServices<ModuleMessageView, ModuleMessageViewSearch>,*/ IViewReadService<UnifiedModuleMessageView, UnifiedModuleMessageViewSearch>
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
        public async Task<UnifiedModuleMessageView> AddMessageAsync(UnifiedModuleMessageView basic, Requester requester) //long senderuid, long receiveruid, string message, string module)
        {
            //This is IF the message is sent to a user
            if(basic.parentId == 0)
            {
                return services.mapper.Map<UnifiedModuleMessageView>(await moduleMessageService.AddMessageAsync(basic));
            }
            else
            {
                return services.mapper.Map<UnifiedModuleMessageView>(await moduleRoomMessageService.WriteAsync(services.mapper.Map<CommentView>(basic), requester));
            }
        }

        public async Task<List<UnifiedModuleMessageView>> SearchAsync(UnifiedModuleMessageViewSearch search, Requester requester)
        {
            if(search.ParentIds.Count == 0)
            {
                var result = await moduleMessageService.SearchAsync(search, requester);
                return result.Select(x => services.mapper.Map<UnifiedModuleMessageView>(x)).ToList();
            }
            else
            {
                var result = await moduleRoomMessageService.SearchAsync(services.mapper.Map<CommentSearch>(search), requester);
                return result.Select(x => services.mapper.Map<UnifiedModuleMessageView>(x)).ToList();
            }
        }
    }
}