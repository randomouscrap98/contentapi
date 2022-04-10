namespace contentapi.Live;

public enum EventType : byte
{
    none = 0,
    message_event = 1,
    activity_event,
    watch_event,
    uservariable_event,
    user_event
}