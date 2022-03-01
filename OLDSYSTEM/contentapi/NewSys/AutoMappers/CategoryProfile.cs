using AutoMapper;
using contentapi.Db;
using contentapi.Views;

namespace contentapi
{
    public class CategoryProfile : Profile
    {
        public CategoryProfile()
        {
            CreateMap<CategoryView, Db.Content_Convert>()
            .ForMember(x => x.literalType, 
                 opt => opt.MapFrom(src => "category")) //We're not using categories anymore
            .ForMember(x => x.text, 
                 opt => opt.MapFrom(src => src.description))
            .ForMember(x => x.contentType, 
                 opt => opt.MapFrom(src => InternalContentType.page))
                 ;
        }
    }
}