using AutoMapper;
using contentapi.Db;
using contentapi.Views;

namespace contentapi
{
    public class ContentProfile : Profile
    {
        public ContentProfile()
        {
            CreateMap<ContentView, Db.Content_Convert>()
            .ForMember(x => x.literalType, 
                 opt => opt.MapFrom(src => src.type))
            .ForMember(x => x.contentType, 
                 opt => opt.MapFrom(src => InternalContentType.page))
                 ;
        }
    }
}