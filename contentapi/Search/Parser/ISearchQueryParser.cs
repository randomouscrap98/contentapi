namespace contentapi.Search;

public interface ISearchQueryParser
{
    bool IsFieldNameValid(string field);
    string ParseQuery(string query, Func<string, string> fieldConverter, Func<string, string> valueConverter);
}