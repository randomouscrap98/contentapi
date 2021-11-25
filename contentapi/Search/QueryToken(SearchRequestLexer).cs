using sly.lexer;

namespace contentapi.Search;

[Lexer(KeyWordIgnoreCase = true)]
public enum QueryToken 
{
    [Lexeme("[a-zA-Z_][a-zA-Z0-9]+")] 
    FIELD = 1,

    //[Lexeme("\\+")] 
    [Lexeme("@[a-zA-Z_][a-zA-Z0-9_\\.]*")]
    VALUE,

    [Lexeme("\\(")] LPAREN,
    [Lexeme("\\)")] RPAREN,

    [Lexeme("<")] LTHAN,
    [Lexeme("<=")] LTHANEQ,
    [Lexeme(">")] GTHAN,
    [Lexeme(">=")] GTHANEQ,
    [Lexeme("==")] EQUALS,
    [Lexeme("<>")] NOTEQUALS,
    [Lexeme("[iI][nN]")] IN,
    [Lexeme("[nN][oO][tT]")] NOT,
    [Lexeme("[lL][iI][kK][eE]")] LIKE,

    [Lexeme("[aA][nN][dD]")] AND,
    [Lexeme("[oO][rR]")] OR,

    [Lexeme("[ \\t]+",isSkippable:true)] // the lexeme is marked isSkippable : it will not be sent to the parser and simply discarded.
    WS
}