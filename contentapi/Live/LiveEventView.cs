using AutoMapper;
using contentapi.Db;

namespace contentapi.Live;

public class LiveEventView
{
    public int id {get;set;}
    public DateTime date {get;set;}
    public long userId {get;set;}
    public UserAction action {get;set;}
    public string type {get;set;} = "";
    public long refId {get;set;}

    public LiveEventView() { }
}

public class EventDataViewProfile : Profile
{
    public EventDataViewProfile()
    {
        CreateMap<LiveEvent, LiveEventView>().ReverseMap();
    }
}