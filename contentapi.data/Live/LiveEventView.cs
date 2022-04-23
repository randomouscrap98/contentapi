using contentapi.Db;

namespace contentapi.data;

public class LiveEventView
{
    public int id {get;set;}
    public DateTime date {get;set;}
    public long userId {get;set;}
    public UserAction action {get;set;}
    public string type {get;set;} = "";
    public long refId {get;set;}

    public LiveEventView() { }
}
