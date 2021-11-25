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
        var buildResult = builder.BuildParser(parserInstance, ParserType.LL_RECURSIVE_DESCENT, "main");
        if(buildResult.IsError)
        {
            var errors = buildResult.Errors?.Select(x => x.Message);
            if(errors == null || errors.Count() == 0)
                errors = new List<string> { "Unknown error" };
            throw new InvalidOperationException("Couldn't construct parser: " + string.Join(",", errors));
        }
        parser = buildResult.Result;
    }

    public string ParseQuery(string query, Func<string, string> fieldConverter, Func<string, string> valueConverter)
    {
        //I don't know how to handle blank stuff; it's allowed, but... egh can't get the
        //grammar to work, having exceptions in their 'left recursion' checker that i don't
        //think are due to left recursion... could be mistaken.
        if(string.IsNullOrWhiteSpace(query))
            return "";

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