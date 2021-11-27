namespace contentapi.stupidity;

public class ThirdPartyStupidity
{
    public static string ExtractHost(HttpRequest request) =>
        request.Headers.ContainsKey("X-Forwarded-Host") ?
            new Uri($"{ExtractProto(request)}://{request.Headers["X-Forwarded-Host"].First()}").Host :
                request.Host.Host;
    public static string ExtractProto(HttpRequest request) =>
        request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Protocol;

    public static string ExtractPath(HttpRequest request) =>
        request.Headers.ContainsKey("X-Forwarded-Host") ?
            new Uri($"{ExtractProto(request)}://{request.Headers["X-Forwarded-Host"].First()}").AbsolutePath :
            string.Empty;
}