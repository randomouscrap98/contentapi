using contentapi.Db;
using contentapi.Utilities;
using contentapi.Views;

namespace contentapi.Main;

public class DbWorkUnit<T> where T : class, IIdView, new()
{
    public T view {get;set;}
    public T? existing {get;set;}
    public UserView requester {get;set;}
    public TypeInfo typeInfo {get;set;}
    public UserAction action {get;set;}

    public string? message {get;set;}

    public DbWorkUnit(T view, UserView requester, TypeInfo tinfo, UserAction action, T? existing = null, string? message = null)
    {
        this.view = view;
        this.requester = requester;
        this.typeInfo = tinfo;
        this.action = action;
        this.existing = existing;
        this.message = message;
    }
}