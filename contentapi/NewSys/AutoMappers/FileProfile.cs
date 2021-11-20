using AutoMapper;
using contentapi.Db;
using contentapi.Views;

namespace contentapi
{
    public class FileProfile : Profile
    {
        public FileProfile()
        {
            CreateMap<FileView, Db.Content>()
            .ForMember(x => x.publicType, //For optimization, public type HAS TO be the bucket. There's an index!
                 opt => opt.MapFrom(src => src.bucket))
            .ForMember(x => x.content, 
                 opt => opt.MapFrom(src => src.fileType)) //Basically, a description of the file
            .ForMember(x => x.internalType, 
                 opt => opt.MapFrom(src => InternalContentType.file))
                 ;
        }
    }
}