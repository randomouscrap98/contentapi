using AutoMapper;
using contentapi.Views;

namespace contentapi
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<UserViewFull, Db.User_Convert>()
                //.ForMember(x => x.hidelist, opt => opt.MapFrom(src => string.Join(",", src.hidelist)))
                .ForMember(x => x.avatar, opt => opt.MapFrom(src => src.avatar.ToString()));
        }
    }
}