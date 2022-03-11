namespace contentapi.Live;

public class WebSocketRequest
{
    public string id {get;set;} = Guid.NewGuid().ToString();
    public string type {get;set;} = "";
    public string token {get;set;} = "";
    public object? data {get;set;} = null;
}