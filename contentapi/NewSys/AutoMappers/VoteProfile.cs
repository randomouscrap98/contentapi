using AutoMapper;
using contentapi.Db;
using contentapi.Services.Constants;
using contentapi.Views;

namespace contentapi
{
    public class VoteProfile : Profile
    {
        public VoteProfile()
        {
            CreateMap<VoteView, Db.ContentVote>()
            .ForMember(x => x.vote, opt => opt.MapFrom(src => VoteType.none));
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