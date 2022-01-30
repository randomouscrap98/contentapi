using AutoMapper;
using contentapi.Utilities;

namespace contentapi.Search;

//This should probably move out at some point
public class SearchRequestPlus : SearchRequest
{
    public RequestType requestType {get;set;}
    public DbTypeInfo typeInfo {get;set;} = new DbTypeInfo();
    public List<string> requestFields {get;set;} = new List<string>();
    public Guid requestId = Guid.NewGuid();
    public string UniqueRequestKey(string field)
    {
        return $"_req{requestId.ToString().Replace("-", "")}_{name}_{field}";
    }

    public string computedSql {get;set;} = "";
}

public class SearchRequestPlusProfile : Profile
{
    public SearchRequestPlusProfile()
    {
        CreateMap<SearchRequest, SearchRequestPlus>().ReverseMap();
    }
}