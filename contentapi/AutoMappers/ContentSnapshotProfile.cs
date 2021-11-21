using AutoMapper;
using contentapi.Db;

namespace contentapi.AutoMapping;

public class ContentSnapshotProfile : Profile
{
    public ContentSnapshotProfile()
    {
        CreateMap<Content, ContentSnapshot>().ReverseMap();
    }
}
