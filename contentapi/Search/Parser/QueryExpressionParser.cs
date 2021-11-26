using sly.lexer;
using sly.parser.generator;

namespace contentapi.Search;

public class QueryExpressionParser
{
    public Func<string, string> HandleValue {get;set;} = s => s;
    public Func<string, string> HandleField {get;set;} = f => f;
    public Func<string, string, string> HandleMacro {get;set;} = (m, a) => $"{m}({a})";
    //public Dictionary<string, Func<string, string>> MacroGenerators {get;set;} = new Dictionary<string, Func<string, string>>();

    [Production("expr: filter AND expr")]
    [Production("expr: filter OR expr")]
    public string ExpressionLogic(string filter, Token<QueryToken> op, string expr)
    {
        return $"{filter} {op.Value} {expr}";
    }

    [Production("expr: filter")]
    public string ExpressionSingle(string filter)
    {
        return filter;
    }

    [Production("filter: FIELD op VALUE")]
    public string Filter(Token<QueryToken> fieldToken, string op, Token<QueryToken> val)
    {
        return $"{HandleField(fieldToken.Value)} {op} {HandleValue(val.Value)}";
    }

    [Production("filter: MACROSTART [d] FIELD LPAREN [d] arglist RPAREN [d]")]
    public string FilterMacro(Token<QueryToken> macroNameToken, string arglist)
    {
        return HandleMacro(macroNameToken.Value, arglist);
    }

    //This is that loop-back thing I don't understand. This was ambiguous when this
    //production was derived from "expr" rather than filter, but... ugh I'm not smart
    [Production("filter: LPAREN expr RPAREN")]
    public string FilterGroup(Token<QueryToken> lparen, string expr, Token<QueryToken> rparen)
    {
        return $"{lparen.Value}{expr}{rparen.Value}";
    }

    [Production("arglist: FIELD COMMA arglist")]
    [Production("arglist: VALUE COMMA arglist")]
    public string ArglistList(Token<QueryToken> field, Token<QueryToken> comma, string rest)
    {
        return $"{field.Value}{comma.Value}{rest}";
    }

    [Production("arglist: FIELD")]
    [Production("arglist: VALUE")]
    public string ArglistSingle(Token<QueryToken> field)
    {
        return field.Value;
    }

    [Production("op: LTHAN")]
    [Production("op: GTHAN")]
    [Production("op: EQUALS")]
    [Production("op: IN")]
    [Production("op: LIKE")]
    public string Operator(Token<QueryToken> opToken)
    {
        return opToken.Value;
    }

    [Production("op: LTHAN EQUALS")]
    [Production("op: GTHAN EQUALS")]
    [Production("op: LTHAN GTHAN")]
    [Production("op: NOT LIKE")]
    [Production("op: NOT IN")]
    public string Operator2(Token<QueryToken> opToken1, Token<QueryToken> opToken2)
    {
        switch(opToken1.TokenID)
        {
            case QueryToken.LTHAN:
            case QueryToken.GTHAN:
            case QueryToken.EQUALS:
                return $"{opToken1.Value}{opToken2.Value}";
            default:
                return $"{opToken1.Value} {opToken2.Value}";
        }
    }
}
