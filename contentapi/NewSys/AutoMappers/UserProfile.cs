using AutoMapper;
using contentapi.Views;

namespace contentapi
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<UserViewFull, Db.User>().ForMember(x => x.hidelist, 
                 opt => opt.MapFrom(src => string.Join(",", src.hidelist)));
        }
    }
}