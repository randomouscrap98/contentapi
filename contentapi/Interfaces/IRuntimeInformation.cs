namespace contentapi;

public interface IRuntimeInformation
{
    DateTime ProcessStart {get;}
    TimeSpan ProcessRuntime {get;}
}