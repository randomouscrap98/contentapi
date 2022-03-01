using AutoMapper;

namespace contentapi.Db.History
{
    public class ContentHistorySnapshotProfile : Profile
    {
        public ContentHistorySnapshotProfile()
        {
            CreateMap<Content, ContentSnapshot>().ReverseMap();
            CreateMap<Message, CommentSnapshot>().ReverseMap();
        }
    }
}

