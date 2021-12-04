namespace contentapi
{
    public class ContentSnapshotProfile : BanProfile
    {
        public ContentSnapshotProfile()
        {
            CreateMap<Db.Content, Db.History.ContentSnapshot>().ReverseMap();
        }
    }
}