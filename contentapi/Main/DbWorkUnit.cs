using contentapi.Db;
using contentapi.Search;
using contentapi.Views;

namespace contentapi.Main;

/// <summary>
/// A unit of work to be PERFORMED on the dictionary. This should represent a MODIFICATION, even if 
/// action can technically be set to "read"
/// </summary>
/// <typeparam name="T"></typeparam>
public class DbWorkUnit<T> where T : class, IIdView, new()
{
    public T view {get;set;}
    public T? existing {get;set;}
    public UserView requester {get;set;}
    public ViewTypeInfo typeInfo {get;set;}
    public UserAction action {get;set;}

    public string? message {get;set;}

    public DbWorkUnit(T view, UserView requester, ViewTypeInfo tinfo, UserAction action, T? existing = null, string? message = null)
    {
        this.view = view;
        this.requester = requester;
        this.typeInfo = tinfo;
        this.action = action;
        this.existing = existing;
        this.message = message;
    }
}