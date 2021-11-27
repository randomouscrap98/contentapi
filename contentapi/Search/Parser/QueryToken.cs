using sly.lexer;

namespace contentapi.Search;

[Lexer(KeyWordIgnoreCase = true)]
public enum QueryToken 
{
    [Lexeme("\\(")] LPAREN = 1,
    [Lexeme("\\)")] RPAREN,

    //Keywords and ops go first? Anyway... these are all "operators" even though they're
    //also some of them just keywords
    [Lexeme("=")] EQUALS,
    [Lexeme("<")] LTHAN,
    [Lexeme(">")] GTHAN,
    [Lexeme("!")] MACROSTART,
    [Lexeme(",")] COMMA,
    [Lexeme("[iI][nN]")] IN,
    [Lexeme("[nN][oO][tT]")] NOT,
    [Lexeme("[lL][iI][kK][eE]")] LIKE,

    [Lexeme("[aA][nN][dD]")] AND,
    [Lexeme("[oO][rR]")] OR,

    //make sure field/etc comes after keywords; keywords should be matched first
    [Lexeme("[a-zA-Z_][a-zA-Z0-9_]+")] FIELD,
    [Lexeme("@[a-zA-Z_][a-zA-Z0-9_\\.]*")] VALUE,


    [Lexeme("[ \\t]+",isSkippable:true)] // the lexeme is marked isSkippable : it will not be sent to the parser and simply discarded.
    WS
}