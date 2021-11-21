using contentapi.Db;

namespace contentapi
{
    public class ContentSnapshotProfile : BanProfile
    {
        public ContentSnapshotProfile()
        {
            CreateMap<Content, ContentSnapshot>().ReverseMap();
        }
    }
}