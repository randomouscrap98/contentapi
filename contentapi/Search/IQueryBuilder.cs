using contentapi.data;

namespace contentapi.Search;

public interface IQueryBuilder
{
    string CombineQueryClause(string baseQuery, string clause);
    SearchRequestPlus FullParseRequest(SearchRequest request, Dictionary<string, object> parameters);
    AboutSearch GetAboutSearch();
}