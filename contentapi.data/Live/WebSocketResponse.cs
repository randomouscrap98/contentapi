namespace contentapi.data;

public class WebSocketResponse
{
    public string id {get;set;} = "";
    public string type {get;set;} = "";
    public long requestUserId {get;set;} = 0;
    public object? data {get;set;} = null;
    public string? error {get;set;} = null;
}