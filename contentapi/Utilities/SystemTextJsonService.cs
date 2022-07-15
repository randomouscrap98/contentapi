using System.Text.Json;

namespace contentapi.Utilities;

public class SystemTextJsonService : IJsonService
{
    protected JsonSerializerOptions options;

    public SystemTextJsonService(JsonSerializerOptions options)
    {
        this.options = options;
    }

    public T? Deserialize<T>(string text)
    {
        return JsonSerializer.Deserialize<T>(text, options);
    }

    public string Serialize(object item)
    {
        return JsonSerializer.Serialize(item, options);
    }

    public object? DeserializeArbitrary(string text)
    {
        return Deserialize<JsonElement>(text);
    }
}