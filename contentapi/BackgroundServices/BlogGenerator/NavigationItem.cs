namespace blog_generator;

public class NavigationItem
{
    public bool current {get;set;}
    public string link {get;set;} = "";
    public string text {get;set;} = "<NO TITLE>";
    public string? hash {get;set;}
    public DateTime? create_date {get;set;}

    public string? create_date_str => Constants.ShortIsoFormat(create_date);
    public string? create_date_short => create_date?.ToShortDateString();
}