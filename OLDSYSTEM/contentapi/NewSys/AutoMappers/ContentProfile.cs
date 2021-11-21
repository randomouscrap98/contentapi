using AutoMapper;
using contentapi.Db;
using contentapi.Views;

namespace contentapi
{
    public class ContentProfile : Profile
    {
        public ContentProfile()
        {
            CreateMap<ContentView, Db.Content>()
            .ForMember(x => x.publicType, 
                 opt => opt.MapFrom(src => src.type))
            .ForMember(x => x.internalType, 
                 opt => opt.MapFrom(src => InternalContentType.page))
                 ;
        }
    }
}