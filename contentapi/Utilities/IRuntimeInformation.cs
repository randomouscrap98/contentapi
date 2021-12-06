namespace contentapi.Utilities;

public interface IRuntimeInformation
{
    DateTime ProcessStart {get;}
    TimeSpan ProcessRuntime {get;}
}