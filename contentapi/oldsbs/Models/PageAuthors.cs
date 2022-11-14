namespace contentapi.oldsbs;

// This table is empty? I don't get it!
public class PageAuthors
{
    //primary key is the page and user. I guess it just says "this user
    //authored this page?" Is the author the one who created the page 
    //or the one who created the program???
    public long pid {get;set;} //the page
    public long uid {get;set;}
    public bool edit {get;set;}
}