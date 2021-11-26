using System;
using contentapi.Search;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

//Use a fixture so the same parser is used for every test, this 
//makes them run much faster (there's a LOT of parser tests)
public class SearchQueryParserTestFixture : UnitTestBase
{
    public SearchQueryParser Parser;

    public SearchQueryParserTestFixture()
    {
        Parser = new SearchQueryParser(GetService<ILogger<SearchQueryParser>>());
    }
}

public class SearchQueryParserTest : UnitTestBase, IClassFixture<SearchQueryParserTestFixture>
{
    protected SearchQueryParser parser;

    public SearchQueryParserTest(SearchQueryParserTestFixture fixture)
    {
        parser = fixture.Parser;
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
    [InlineData("someTest", true)]
    [InlineData("some_Test", true)]
    [InlineData("_some_Test", true)]
    [InlineData("some.Test", false)]
    [InlineData(";thing", false)]
    [InlineData("thing;", false)]
    [InlineData("1wo", false)]
    public void SearchQueryParser_IsFieldNameValid(string field, bool valid)
    {
        if(valid)
            Assert.True(parser.IsFieldNameValid(field), $"{field} was supposed to be valid!");
        else
            Assert.False(parser.IsFieldNameValid(field), $"{field} was supposed to be invalid!");
    }

    [Theory]
    [InlineData("field > @num", true)] //Just testing all the operators, had trouble with these 
    [InlineData("field < @num", true)]
    [InlineData("field >= @num", true)]
    [InlineData("field <= @num", true)]
    [InlineData("field <> @num", true)]
    [InlineData("field = @num", true)]
    [InlineData("field IN @num", true)] //If these "keyword" ops fail, the order of lexing could be to blame
    [InlineData("field NOT IN @num", true)]
    [InlineData("field LIKE @num", true)]
    [InlineData("field NOT LIKE @num", true)]
    [InlineData("THING_THING = @num", true)] //Now test the field regex
    [InlineData("_field = @num", true)]
    [InlineData("1field = @num", false)]
    [InlineData("field.br = @num", false)]
    [InlineData("124 = @num", false)]
    [InlineData("field = @a.b.c", true)] //Now to test the value regex
    [InlineData("field = @_a._b._c", true)]
    [InlineData("field = @@a", false)]
    [InlineData("field = 123", false)]  //Don't want to use literals or non-annotated variables
    [InlineData("field = \"value\"", false)] 
    [InlineData("field = 'value'", false)]
    [InlineData("field = value", false)]
    [InlineData("", true)] //Specifically empty string needs to work
    [InlineData("  ", true)]
    [InlineData("field NOT > @num", false)] //Fun combinations
    [InlineData("field LIKE NOT @num", false)] 
    [InlineData("field NOT = @num", false)] 
    [InlineData("field != @num", false)] 
    [InlineData("field field", false)] 
    [InlineData("field > @num;", false)] //AN IMPORTANT ONE!
    [InlineData(";DROP TABLE users", false)] //AN IMPORTANT ONE!
    [InlineData("\";drop table users", false)] //AN IMPORTANT ONE!
    [InlineData("field > @num1 and field < @num2", true)] //Getting into the and/or stuff
    [InlineData("username LIKE @search and id IN @subset", true)]
    [InlineData("(username LIKE @search)", true)]
    [InlineData("(username LIKE @search", false)]
    [InlineData("username LIKE @search)", false)]
    [InlineData("()", false)]
    [InlineData("username LIKE @search AND ()", false)]
    [InlineData("username LIKE @search AND (contentId in @pages.cids or action = @MYACT)", true)]
    [InlineData("(username LIKE @search and (createDate > @longTimeAgo)) AND (contentId in @pages.cids or action = @MYACT)", true)]
    [InlineData("(username LKE @search and (createDate > @longTimeAgo)) AND (contentId in @pages.cids or action = @MYACT)", false)]
    [InlineData("(username LIKE @search and createDate > @longTimeAgo)) AND (contentId in @pages.cids or action = @MYACT)", false)]
    [InlineData("(username LIKE @search and (createDate @longTimeAgo)) AND (contentId in @pages.cids or action = @MYACT)", false)]
    [InlineData("(username LIKE @search and (createDate > @longTimeAgo)) (contentId in @pages.cids or action = @MYACT)", false)]
    public void SearchQueryParser_SyntaxCheck(string query, bool success)
    {
        string result = "";
        try
        {
            result = parser.ParseQuery(query, f => f, v => v);
            Assert.True(success, $"Query '{query}' was supposed to fail!");
        }
        catch(Exception ex)
        {
            Assert.False(success, $"Query '{query}' should not have failed: Error: {ex.Message}");
            return; //Don't bother with the rest of the checks, it failed (and was suppsoed to)
        }

        if(string.IsNullOrWhiteSpace(query))
            Assert.True(string.IsNullOrWhiteSpace(result));
        else
            Assert.Equal(query, result);
    }

    [Theory]
    [InlineData("!macro(value)", "macro(value)")]
    [InlineData("(!macro(value))", "(macro(value))")]
    [InlineData("!macro(value, value2)", "macro(value,value2)")]
    [InlineData("!macro(value, value2, _value_3)", "macro(value,value2,_value_3)")]
    [InlineData("!CRAZY_STUFF(WoWO_, ___, eys)", "CRAZY_STUFF(WoWO_,___,eys)")]
    [InlineData("!not.macro(value)", null)]
    [InlineData("!notmacro(value.value)", null)]
    [InlineData("!yesmacro(@value)", "yesmacro(@value)")]
    [InlineData("!yesmacro(@value.deeper)", "yesmacro(@value.deeper)")]
    [InlineData("field > !notmacro(@value)", null)]
    [InlineData("field and !notmacro(@value)", null)]
    [InlineData("!notmacro(@value) and field", null)]
    [InlineData("field = @value and !macro(@value)", "field = @value and macro(@value)")]
    [InlineData("(field = @value) and (!macro(@value))", "(field = @value) and (macro(@value))")]
    [InlineData("(field = @value) and !macro(@value)", "(field = @value) and macro(@value)")]
    [InlineData("field = @value and (!macro(@value))", "field = @value and (macro(@value))")]
    [InlineData("((field = @value) and (!macro(@value)))", "((field = @value) and (macro(@value)))")]
    public void SearchQueryParser_MacroCheck(string query, string? expected)
    {
        string result = "";
        try
        {
            result = parser.ParseQuery(query, f => f, v => v);
            Assert.True(expected != null, $"Query '{query}' was supposed to fail!");
        }
        catch(Exception ex)
        {
            Assert.True(expected == null, $"Query '{query}' should not have failed: Error: {ex.Message}");
            return; //Don't bother with the rest of the checks, it failed (and was suppsoed to)
        }

        if(string.IsNullOrWhiteSpace(query))
            Assert.True(string.IsNullOrWhiteSpace(result));
        else
            Assert.Equal(expected, result);
    }
}