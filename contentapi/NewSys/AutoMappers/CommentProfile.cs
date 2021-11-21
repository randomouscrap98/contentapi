using AutoMapper;
using contentapi.Views;

namespace contentapi
{
    public class CommentProfile : Profile
    {
        public CommentProfile()
        {
            //Convert everything into the new "unifiedmodulemessage" before convverting to message
            CreateMap<CommentView, Db.Comment>()
                .ForMember(x => x.contentId, opt => opt.MapFrom(src => src.parentId))
                .ForMember(x => x.text, opt => opt.MapFrom(src => src.content))
            ;
        }
    }
}