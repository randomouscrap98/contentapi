namespace contentapi.Live;

public enum EventType : byte
{
    none = 0,
    message = 1,
    activity,
    watch,
    uservariable,
    user,
    userlist
}