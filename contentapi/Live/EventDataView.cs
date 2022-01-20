using AutoMapper;
using contentapi.Db;

namespace contentapi.Live;

public class EventDataView
{
    public int id {get;set;}
    public DateTime date {get;set;}
    public long userId {get;set;}
    public UserAction action {get;set;}
    public EventType type {get;set;}
    public long refId {get;set;}

    public EventDataView() { }
}

public class EventDataViewProfile : Profile
{
    public EventDataViewProfile()
    {
        CreateMap<EventData, EventDataView>().ReverseMap();
    }
}