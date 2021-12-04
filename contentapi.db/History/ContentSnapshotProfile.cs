using AutoMapper;

namespace contentapi.Db.History
{
    public class ContentSnapshotProfile : Profile
    {
        public ContentSnapshotProfile()
        {
            CreateMap<Content, ContentSnapshot>().ReverseMap();
        }
    }
}

