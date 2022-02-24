using AutoMapper;
using contentapi.Db;
using contentapi.Views;

namespace contentapi
{
    public class FileProfile : Profile
    {
        public FileProfile()
        {
            CreateMap<FileView, Db.Content_Convert>()
            .ForMember(x => x.extra1,
                opt => opt.MapFrom(src => src.quantization.ToString()))
            .ForMember(x => x.name, 
                 opt => opt.MapFrom(src => src.name ?? "")) //Basically, a description of the file
            .ForMember(x => x.publicType, //The public type is the lookup hash, which has to be our id since there's no hash from before and we don't want to break our links
                 opt => opt.MapFrom(src => src.id.ToString()))
            .ForMember(x => x.text, 
                 opt => opt.MapFrom(src => src.fileType)) //Basically, a description of the file
            .ForMember(x => x.internalType, 
                 opt => opt.MapFrom(src => InternalContentType.file))
                 ;
        }
    }
}