
using AutoMapper;
using contentapi.Db;
using contentapi.Services.Constants;
using contentapi.Views;

namespace contentapi
{
    public class WatchProfile : Profile
    {
        public WatchProfile()
        {
            CreateMap<WatchView, Db.ContentWatch>()
            .ForMember(x => x.lastCommentId, opt => opt.MapFrom(src => src.lastNotificationId))
            .ForMember(x => x.lastActivityId, opt => opt.MapFrom(src => 0)) //This will reset a lot of watches, everyone will have lots of notifications
            .ForMember(x => x.editDate, opt => opt.MapFrom(src => src.createDate))
            ;
            //.ForMember(x => x.vote, 
            //     opt => opt.MapFrom(src => 
            //     {
            //         var v = src.vote.ToLower();
            //         if(v == "b") return VoteType.bad;
            //         if(v == "o") return VoteType.ok;
            //         if(v == "g") return VoteType.good;
            //         return VoteType.none;
            //     }));
        }
    }
}