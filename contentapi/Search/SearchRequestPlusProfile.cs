using AutoMapper;

namespace contentapi.Search;

public class SearchRequestPlusProfile : Profile
{
    public SearchRequestPlusProfile()
    {
        CreateMap<SearchRequest, SearchRequestPlus>().ReverseMap();
    }
}
