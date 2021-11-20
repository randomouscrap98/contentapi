using AutoMapper;
using contentapi.Db;
using contentapi.Views;

namespace contentapi
{
    public class CategoryProfile : Profile
    {
        public CategoryProfile()
        {
            CreateMap<CategoryView, Db.Content>()
            .ForMember(x => x.publicType, 
                 opt => opt.MapFrom(src => ""))
            .ForMember(x => x.content, 
                 opt => opt.MapFrom(src => src.description))
            .ForMember(x => x.internalType, 
                 opt => opt.MapFrom(src => InternalContentType.category))
                 ;
        }
    }
}