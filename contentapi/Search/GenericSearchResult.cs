namespace contentapi.Search;

public class GenericSearchResult
{
    public SearchRequests? search {get;set;}
    public Dictionary<string, double> databaseTimes {get;set;} = new Dictionary<string, double>();
    public Dictionary<string, IEnumerable<IDictionary<string, object>>> objects {get;set;} = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
}