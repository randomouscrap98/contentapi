namespace contentapi.Search;

/// <summary>
/// A single search request, for retrieving data from the database (sometimes tailored)
/// </summary>
public class SearchRequest
{
    public string name {get;set;} = ""; // you must name your request so it can be referenced
    public string type {get;set;} = "";
    public string fields {get;set;} = "";
    public string query {get;set;} = "";
    public string order {get;set;} = ""; //_desc for descending
    public int limit {get;set;} = -1;
    public int skip {get;set;}

    public SearchRequest Copy()
    {
        return (SearchRequest)this.MemberwiseClone();
    }
}

/// <summary>
/// All requests, which could be related (chained) to each other. Any values need to be named in the values dictionary
/// </summary>
public class SearchRequests
{
    public Dictionary<string, object> values {get;set;} = new Dictionary<string, object>();
    public List<SearchRequest> requests {get;set;} = new List<SearchRequest>();

    public SearchRequests Copy()
    {
        return new SearchRequests()
        {
            values = new Dictionary<string, object>(values),
            requests = requests.Select(x => x.Copy()).ToList()
        };
    }
}
