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
            .ForMember(x => x.hash, //The public type is the lookup hash, which has to be our id since there's no hash from before and we don't want to break our links
                 opt => opt.MapFrom(src => src.id.ToString()))
            .ForMember(x => x.contentType, 
                 opt => opt.MapFrom(src => InternalContentType.page))
                 ;
        }
    }
}