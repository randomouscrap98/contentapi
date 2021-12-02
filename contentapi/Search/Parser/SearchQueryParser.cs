using sly.parser;
using sly.parser.generator;

namespace contentapi.Search;

public class SearchQueryParser : ISearchQueryParser
{
    protected ILogger logger;
    protected QueryExpressionParser parserInstance;
    protected Parser<QueryToken, string> parser;
    protected readonly object parserLock = new Object();

    public SearchQueryParser(ILogger<SearchQueryParser> logger)
    {
        this.logger = logger;

        //Sly isn't designed with DI in mind, so... just hide it inside our own
        //dependency injection interface thing. Doesn't matter that we're directly instantiating it
        parserInstance = new QueryExpressionParser(); 

        var builder = new ParserBuilder<QueryToken, string>();
        var buildResult = builder.BuildParser(parserInstance, ParserType.LL_RECURSIVE_DESCENT, "expr");
        if(buildResult.IsError)
        {
            var errors = buildResult.Errors?.Select(x => x.Message);
            if(errors == null || errors.Count() == 0)
                errors = new List<string> { "Unknown error" };
            throw new InvalidOperationException("Couldn't construct parser: " + string.Join(",", errors));
        }
        parser = buildResult.Result;
    }

    public bool IsFieldNameValid(string field)
    {
        var tokens = parser.Lexer.Tokenize(field);
        //It's 2 tokens because the last one is "end"
        return tokens != null && tokens.IsOk && tokens.Tokens.Count == 2 && tokens.Tokens.First().TokenID == QueryToken.FIELD;
    }

    public string ParseQuery(string query, Func<string, string> fieldConverter, Func<string, string> valueConverter,
        Func<string, string, string> macroHandler)
    {
        //I don't know how to handle blank stuff; it's allowed, but... egh can't get the
        //grammar to work, having exceptions in their 'left recursion' checker that i don't
        //think are due to left recursion... could be mistaken.
        if(string.IsNullOrWhiteSpace(query))
            return "";

        //We lock to make this threadsafe: the parser doesn't allow us to pass in configuration
        //per-call and have it trickle down into the production grammar handlers, so we have
        //to instead pass the config as properties. Because it's a property, we don't want 
        //someone ELSE to come in and change those handlers out from underneath us. If this
        //becomes a performance issue, idk
        lock(parserLock)
        {
            var oldFieldConv = parserInstance.HandleField;
            var oldValueConv = parserInstance.HandleValue;
            var oldMacroHandler = parserInstance.HandleMacro;

            try
            {
                parserInstance.HandleField = fieldConverter;
                parserInstance.HandleValue = valueConverter;
                parserInstance.HandleMacro = macroHandler;

                var result = parser.Parse(query);

                if (result.IsError)
                {
                    if (result.Errors != null && result.Errors.Any())
                        throw new ParseException("ERROR DURING QUERY PARSE: " + string.Join("\n", result.Errors.Select(x => x.ErrorMessage)));
                    else
                        throw new ParseException("Unknown error during query parse");
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
                parserInstance.HandleMacro= oldMacroHandler;
            }
        }
    }
}