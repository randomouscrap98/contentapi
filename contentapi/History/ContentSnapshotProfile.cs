using AutoMapper;
using contentapi.Db;

namespace contentapi.History;

public class ContentHistorySnapshotProfile : Profile
{
    public ContentHistorySnapshotProfile()
    {
        CreateMap<Content, ContentSnapshot>().ReverseMap();
        CreateMap<Message, CommentSnapshot>().ReverseMap();
    }
}
