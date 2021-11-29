using AutoMapper;
using contentapi.Utilities;

namespace contentapi.Search;

//This should probably move out at some point
public class SearchRequestPlus : SearchRequest
{
    //public Db.User requester {get;set;} = new Db.User();
    public RequestType requestType {get;set;}
    public TypeInfo typeInfo {get;set;} = new TypeInfo();
    public List<string> requestFields {get;set;} = new List<string>();
    public Guid requestId = Guid.NewGuid();
    //public Guid globalRequestId {get;set;} = Guid.Empty;
    public string UniqueRequestKey(string field)
    {
        return $"_req{requestId.ToString().Replace("-", "")}_{name}_{field}";
    }

    public string computedSql {get;set;} = "";
    //public string GlobalRequestKey(string field)
    //{
    //    return $"_req{globalRequestId.ToString().Replace("-", "")}_{field}";
    //}
    //public string RequesterKey()
    //{
    //    return GlobalRequestKey("requesterID");
    //}
}

public class SearchRequestPlusProfile : Profile
{
    public SearchRequestPlusProfile()
    {
        CreateMap<SearchRequest, SearchRequestPlus>().ReverseMap();
    }
}