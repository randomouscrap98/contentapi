namespace contentapi.data;

public class WebSocketRequest
{
    public string id {get;set;} = Guid.NewGuid().ToString();
    public string type {get;set;} = "";
    public object? data {get;set;} = null;
}