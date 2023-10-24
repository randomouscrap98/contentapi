namespace contentapi.Utilities;

public class MyRuntimeInformation : IRuntimeInformation
{
    protected DateTime _processStart;

    public MyRuntimeInformation(DateTime start)
    {
        _processStart = start;
        //this.storage = storage;
        //storage.Set(Constants.StorageKeys.restarts.ToString(), storage.Get<int>(Constants.StorageKeys.restarts.ToString(), 0) + 1);
    }

    public DateTime ProcessStart => _processStart;
    public TimeSpan ProcessRuntime => DateTime.Now - _processStart;
}