namespace contentapi;

public class WhereExpression
{
    public string field {get;set;} = "";
    public string op {get;set;} = "";
    public object? value {get;set;}
}