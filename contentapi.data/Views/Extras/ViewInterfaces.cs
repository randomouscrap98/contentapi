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
    long userId { get; set; }
    string type { get; set; }
    string engagement { get; set; }
    DateTime createDate { get; set; }
    long relatedId {get;}

    void SetRelatedId(long id);
}