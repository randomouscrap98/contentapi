using sly.lexer;
using sly.parser.generator;

namespace contentapi.Search;

public class QueryExpressionParser
{
    public Func<string, string> HandleValue {get;set;} = s => s;
    public Func<string, string> HandleField {get;set;} = f => f;

    [Production("op: LTHAN")]
    [Production("op: GTHAN")]
    [Production("op: LTHANQ")]
    [Production("op: GTHANQ")]
    [Production("op: EQUAlS")]
    [Production("op: NOTEQUAlS")]
    [Production("op: IN")]
    [Production("op: NOT IN")]
    [Production("op: LIKE")]
    public string Operator(Token<QueryToken> opToken)
    {
        return opToken.Value;
    }

    [Production("filter: FIELD op VALUE")]
    public string Filter(Token<QueryToken> fieldToken, string op, Token<QueryToken> val)
    {
        return $"{HandleField(fieldToken.Value)} {op} {HandleValue(val.Value)}";
    }

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

    [Production("expr: LPAREN expr RPAREN")]
    public string ExpressionGroup(Token<QueryToken> lparen, string expr, Token<QueryToken> rparen)
    {
        return $"{lparen.Value}{expr}{rparen.Value}";
    }

    [Production("main: expr")]
    public string MainExpression(string expr)
    {
        return expr;
    }

    [Production("main: ")]
    public string MainEmpty()
    {
        return "";
    }
}
