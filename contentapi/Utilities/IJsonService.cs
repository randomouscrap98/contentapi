namespace contentapi.Utilities;

public interface IJsonService
{
    public string Serialize(object item);
    public T? Deserialize<T>(string text);
    public object? DeserializeArbitrary(string text);
}