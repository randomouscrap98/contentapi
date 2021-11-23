
using AutoMapper;
using contentapi;
using contentapi.Implementations;

public class SearchRequestPlusProfile : Profile
{
    public SearchRequestPlusProfile()
    {
        CreateMap<SearchRequest, SearchRequestPlus>().ReverseMap();
    }
}
