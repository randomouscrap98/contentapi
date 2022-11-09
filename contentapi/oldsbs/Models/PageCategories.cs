namespace contentapi.oldsbs;

//WARN: pages can be part of multiple categories maybe?? I think so yes...
//yes, pages can be in multiple categories. Which means the parent
//of the page is NOT the category, and it'll have to be... a value? ew,
//that's slow to lookup.
public class PageCategories
{
    public long pid {get;set;} // the page
    public long cid {get;set;} // the category
}