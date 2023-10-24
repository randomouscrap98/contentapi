namespace contentapi.Utilities;

public interface IValueStore : IDisposable
{
    public T Get<T>(string key, T defaultValue);
    public void Set<T>(string key, T value);
}