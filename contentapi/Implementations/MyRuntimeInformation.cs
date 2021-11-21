namespace contentapi.Implementations;

public class MyRuntimeInformation : IRuntimeInformation
{
    protected DateTime _processStart;

    public MyRuntimeInformation(DateTime start)
    {
        _processStart = start;
    }

    public DateTime ProcessStart => _processStart;
    public TimeSpan ProcessRuntime => DateTime.Now - _processStart;
}