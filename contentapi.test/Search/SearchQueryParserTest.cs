using System;
using contentapi.Search;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class SearchQueryParserTest : UnitTestBase
{
    protected SearchQueryParser parser;

    public SearchQueryParserTest()
    {
        parser = new SearchQueryParser(GetService<ILogger<SearchQueryParser>>());
    }

    //This is just a VERY BASIC test to see if the parser was even able to be put
    //together properly. This first test, with no parser calls, will throw an exception
    //if "sly" was unable to construct a parser from our grammar.
    [Fact]
    public void SearchQueryParser_Constructed()
    {
        Assert.NotNull(parser);
    }

    [Theory]
    [InlineData("field > @num", true)]
    public void SearchQueryParser_SyntaxCheck(string query, bool success)
    {
        try
        {
            var result = parser.ParseQuery(query, f => f, v => v);
            Assert.True(success, $"Query {query} was supposed to fail!");
            Assert.Equal(query, result);
        }
        catch(Exception ex)
        {
            Assert.False(success, $"Query {query} should not have failed: Error: {ex}");
        }
    }
}