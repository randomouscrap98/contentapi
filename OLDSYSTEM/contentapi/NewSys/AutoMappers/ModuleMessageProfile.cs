using AutoMapper;
using contentapi.Views;

namespace contentapi
{
    public class ModuleMessageProfile : Profile
    {
        public ModuleMessageProfile()
        {
            //Convert everything into the new "unifiedmodulemessage" before convverting to message
            CreateMap<UnifiedModuleMessageView, Db.Message_Convert>()
                .ForMember(x => x.createUserId, opt => opt.MapFrom(src => src.sendUserId))
                .ForMember(x => x.contentId, opt => opt.MapFrom(src => src.parentId))
                .ForMember(x => x.text, opt => opt.MapFrom(src => src.message))
            ;
        }
    }
}