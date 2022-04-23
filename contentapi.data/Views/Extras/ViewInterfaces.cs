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