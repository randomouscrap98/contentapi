namespace contentapi.Utilities;

public class MyRuntimeInformation : IRuntimeInformation
{
    protected DateTime _processStart;
    protected IValueStore storage;

    public MyRuntimeInformation(DateTime start, IValueStore storage)
    {
        _processStart = start;
        this.storage = storage;
        storage.Set(Constants.StorageKeys.restarts.ToString(), storage.Get<int>(Constants.StorageKeys.restarts.ToString(), 0) + 1);
    }

    public DateTime ProcessStart => _processStart;
    public TimeSpan ProcessRuntime => DateTime.Now - _processStart;
}