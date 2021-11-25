using contentapi.Search;
using Microsoft.Extensions.Logging;

namespace contentapi.test;

public class SearchQueryParserTest : UnitTestBase
{
    protected SearchQueryParser parser;

    public SearchQueryParserTest()
    {
        parser = new SearchQueryParser(GetService<ILogger<SearchQueryParser>>());
    }

}