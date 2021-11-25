namespace contentapi.Search;

public interface ISearchQueryParser
{
    string ParseQuery(string query, Func<string, string> fieldConverter, Func<string, string> valueConverter);
}