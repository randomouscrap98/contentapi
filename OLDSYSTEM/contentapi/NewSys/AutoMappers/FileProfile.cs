using AutoMapper;
using contentapi.Db;
using contentapi.Views;
using Newtonsoft.Json;

namespace contentapi
{
    public class FileProfile : Profile
    {
        public FileProfile()
        {
            CreateMap<FileView, Db.Content_Convert>()
            .ForMember(x => x.meta,
                opt => opt.MapFrom(src => JsonConvert.SerializeObject(new { quantize = src.quantization })))
            .ForMember(x => x.name, 
                 opt => opt.MapFrom(src => src.name ?? "")) //Basically, a description of the file
            .ForMember(x => x.hash, //The public type is the lookup hash, which has to be our id since there's no hash from before and we don't want to break our links
                 opt => opt.MapFrom(src => src.id.ToString()))
            .ForMember(x => x.literalType, 
                 opt => opt.MapFrom(src => src.fileType))
            .ForMember(x => x.contentType, 
                 opt => opt.MapFrom(src => InternalContentType.file))
                 ;
        }
    }
}