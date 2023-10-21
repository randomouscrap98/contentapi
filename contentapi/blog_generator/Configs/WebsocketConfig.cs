namespace blog_generator.Configs;

public class WebsocketConfig
{
    public string WebsocketEndpoint {get;set;} = "";
    public string AnonymousToken {get;set;} = "";
    public TimeSpan ReconnectInterval {get;set;} = TimeSpan.FromSeconds(30);
}