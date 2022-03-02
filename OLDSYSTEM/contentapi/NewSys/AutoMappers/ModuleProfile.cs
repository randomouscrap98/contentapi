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
            .ForMember(x => x.description,
                opt => opt.MapFrom(src => src.description))
            .ForMember(x => x.literalType, 
                 opt => opt.MapFrom(src => ""))
            .ForMember(x => x.text, 
                 opt => opt.MapFrom(src => src.code))
            .ForMember(x => x.contentType, 
                 opt => opt.MapFrom(src => InternalContentType.module))
                 ;
        }
    }
}