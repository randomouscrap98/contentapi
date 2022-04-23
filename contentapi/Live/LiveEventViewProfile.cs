using AutoMapper;
using contentapi.data;

namespace contentapi.Live;

public class EventDataViewProfile : Profile
{
    public EventDataViewProfile()
    {
        CreateMap<LiveEvent, LiveEventView>().ReverseMap();
    }
}