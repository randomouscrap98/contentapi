using contentapi.data.Views;

namespace blog_generator;

public class MainTemplateData
{
    public ContentView? page {get;set;}
    public ContentView? parent {get;set;}
    public ActivityView? revision {get;set;}
    public List<string> scripts {get;set;} = new List<string>();
    public List<string> styles {get;set;} = new List<string>();

    public List<NavigationItem> navlinks {get;set;} = new List<NavigationItem>();

    public string version {get;set;} = "1";
    public DateTime render_date {get;set;}
    public string author {get;set;} = "???";
    public string edit_author {get;set;} = "???";
    public string keywords {get;set;} = "";
    public string parent_link {get;set;} = "#";

    public bool is_parent => parent?.id == page?.id;
    public bool is_edited => revision?.action == contentapi.data.UserAction.update;


    //public PageIcon? icon {get;set;} = null;

    public string? render_date_str => Constants.ShortIsoFormat(render_date);
    public string? page_create_date_str => Constants.ShortIsoFormat(page?.createDate);
    public string? page_edit_date_str => Constants.ShortIsoFormat(revision?.date);
}
