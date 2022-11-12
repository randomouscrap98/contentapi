namespace contentapi.data.Views;

public interface IIdView
{
    long id {get;set;}
}

public interface IContentRelatedView : IIdView
{
    long contentId {get;set;}
}

public interface IContentUserRelatedView : IContentRelatedView
{
    long userId {get;set;}
}

public interface IEngagementView : IIdView
{
    public long userId { get; set; }
    public string type { get; set; }
    public string engagement { get; set; }
    public DateTime createDate { get; set; }
}