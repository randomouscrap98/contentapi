using System.Collections.Concurrent;

namespace contentapi.Utilities;

/// <summary>
/// The event tracker is a simple event TIME keeper. It tracks when things occur, and lets 
/// you count how many happened in a given time. useful for rate limiting
/// </summary>
public interface IEventTracker
{
    /// <summary>
    /// Record that the given event took place right now
    /// </summary>
    /// <param name="eventKey"></param>
    public void AddEvent(string eventKey);

    /// <summary>
    /// Count how many of the given event have happened in the given time
    /// </summary>
    /// <param name="eventKey"></param>
    /// <param name="time"></param>
    /// <returns></returns>
    public int CountEvents(string eventKey, TimeSpan time);
}