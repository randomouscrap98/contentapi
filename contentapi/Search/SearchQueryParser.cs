using sly.parser;
using sly.parser.generator;

namespace contentapi.Search;

public class SearchQueryParser : ISearchQueryParser
{
    protected ILogger logger;
    protected QueryExpressionParser parserInstance;
    protected Parser<QueryToken, string> parser;

    public SearchQueryParser(ILogger<SearchQueryParser> logger)
    {
        this.logger = logger;

        //Sly isn't designed with DI in mind, so... just hide it inside our own
        //dependency injection interface thing. Doesn't matter that we're directly instantiating it
        parserInstance = new QueryExpressionParser(); 

        var builder = new ParserBuilder<QueryToken, string>();
        parser = builder.BuildParser(parserInstance, ParserType.LL_RECURSIVE_DESCENT, "main").Result;
    }

    public string ParseQuery(string query, Func<string, string> fieldConverter, Func<string, string> valueConverter)
    {
        var oldFieldConv = parserInstance.HandleField;
        var oldValueConv = parserInstance.HandleValue;

        try
        {
            parserInstance.HandleField = fieldConverter;
            parserInstance.HandleValue = valueConverter;

            var result = parser.Parse(query);

            if(result.IsError)
            {
                if (result.Errors != null && result.Errors.Any())
                    throw new ArgumentException("ERROR DURING QUERY PARSE: " + string.Join("\n", result.Errors.Select(x => x.ErrorMessage)));
                else
                    throw new ArgumentException("Unknown error during query parse");
            }
            else
            {
                return result.Result;
            }
        }
        finally
        {
            parserInstance.HandleField = oldFieldConv;
            parserInstance.HandleValue = oldValueConv;
        }
    }
}