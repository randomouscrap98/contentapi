using contentapi.data.Views;

namespace blog_generator;

public class StyleData
{
    public ContentView? page {get;set;}
    public string author {get;set;} = "";
    public DateTime render_date {get;set;} = DateTime.UtcNow;
}