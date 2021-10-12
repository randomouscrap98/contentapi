using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class UnifiedModuleMessageViewSearch : ModuleMessageViewSearch
    {
        public List<long> ParentIds {get;set;} = new List<long>();
    }

    public class UnifiedModuleMessageViewServiceProfile : Profile
    {
        public const string ModuleNameSplitChar = "|";

        public UnifiedModuleMessageViewServiceProfile()
        {
            CreateMap<ModuleMessageView, UnifiedModuleMessageView>().ReverseMap();
            CreateMap<UnifiedModuleMessageViewSearch, ModuleMessageViewSearch>().ReverseMap();

            CreateMap<UnifiedModuleMessageViewSearch, CommentSearch>()
                .ForMember(x => x.ContentLike, o => o.MapFrom(s => s.ModuleLike)) //Module messages that are comments start with the module name anyway, so this... might be safe?
                .ReverseMap();

            CreateMap<UnifiedModuleMessageView, CommentView>()
                .ForMember(x => x.createUserId, o => o.MapFrom(s => s.sendUserId))
                .ForMember(x => x.content, o => o.MapFrom(s => $"{s.module}{ModuleNameSplitChar}{s.message}"));
            CreateMap<CommentView, UnifiedModuleMessageView>()
                .ForMember(x => x.sendUserId, o => o.MapFrom(s => s.createUserId))
                .ForMember(x => x.module, o => o.MapFrom(s => s.content.Substring(0, s.content.IndexOf(ModuleNameSplitChar))))
                .ForMember(x => x.message, o => o.MapFrom(s => s.content.Substring(s.content.IndexOf(ModuleNameSplitChar) + ModuleNameSplitChar.Length)));
                //.ForMember(x => x.parentId, o => o.MapFrom(s => s.parentId))
                //.ReverseMap();

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

        public Task<EntityPackage> CanUserDoOnParent(long parentId, string action, Requester requester)
        {
            return moduleRoomMessageService.CanUserDoOnParent(parentId, action, requester);
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