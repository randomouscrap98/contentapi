namespace contentapi.Search;

public interface IQueryBuilder
{
    string CombineQueryClause(string baseQuery, string clause);
    //List<RequestType> ContentRequestTypes {get;}
    SearchRequestPlus FullParseRequest(SearchRequest request, Dictionary<string, object> parameters);
    AboutSearch GetAboutSearch();
}