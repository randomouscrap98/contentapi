using AutoMapper;
using contentapi.Views;

namespace contentapi
{
    public class BanProfile : Profile
    {
        public BanProfile()
        {
            CreateMap<BanView, Db.Ban>();
            //.ForMember(x => x.hidelist, 
            //     opt => opt.MapFrom(src => string.Join(",", src.hidelist)));
        }
    }
}