namespace contentapi.Live;

public class WebSocketResponse
{
    public string id {get;set;} = "";
    public string type {get;set;} = "";
    public object? data {get;set;} = null;
    public string? error {get;set;} = null;
}