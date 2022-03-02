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
            .ForMember(x => x.hash, //The public type is the lookup hash, which has to be our id since there's no hash from before and we don't want to break our links
                 opt => opt.MapFrom(src => src.id.ToString()))
            .ForMember(x => x.text, 
                 opt => opt.MapFrom(src => src.description))
            .ForMember(x => x.description, 
                 opt => opt.MapFrom(src => ""))
            .ForMember(x => x.contentType, 
                 opt => opt.MapFrom(src => InternalContentType.page))
                 ;
        }
    }
}