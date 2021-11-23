namespace contentapi;

public interface IGenericSearch
{
    Task<Dictionary<string, object>> Search(SearchRequests requests);
}