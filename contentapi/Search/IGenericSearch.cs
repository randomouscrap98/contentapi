namespace contentapi.Search;

public interface IGenericSearch
{
    Task<Dictionary<string, object>> Search(SearchRequests requests);
}