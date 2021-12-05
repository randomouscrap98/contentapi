using AutoMapper;
using contentapi.Db;
using contentapi.Views;

namespace contentapi
{
    public class ModuleProfile : Profile
    {
        public ModuleProfile()
        {
            CreateMap<ModuleView, Db.Content_Convert>()
            .ForMember(x => x.extra1,
                opt => opt.MapFrom(src => src.description))
            .ForMember(x => x.publicType, 
                 opt => opt.MapFrom(src => ""))
            .ForMember(x => x.content, 
                 opt => opt.MapFrom(src => src.code))
            .ForMember(x => x.internalType, 
                 opt => opt.MapFrom(src => InternalContentType.module))
                 ;
        }
    }
}