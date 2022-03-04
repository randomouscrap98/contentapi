using System;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class EventTrackerTest : UnitTestBase
{
    protected EventTracker tracker;
    protected EventTrackerConfig config;

    public EventTrackerTest()
    {
        config = new EventTrackerConfig();
        tracker = new EventTracker(GetService<ILogger<EventTracker>>(), config);
    }

    [Fact]
    public void AddEvent_NoFail()
    {
        //Just make sure no exceptions are thrown
        tracker.AddEvent("thing");
    }

    [Fact]
    public void AddEvent_CountEvent()
    {
        tracker.AddEvent("thing");
        var count = tracker.CountEvents("thing", TimeSpan.FromSeconds(10));

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountEvents_None()
    {
        //Make sure that if we read BEFORE writing, that's also ok
        var count = tracker.CountEvents("thing", TimeSpan.FromSeconds(10));
        Assert.Equal(0, count);
    }

    [Fact]
    public void AddEvent_CountDifferentEvent()
    {
        tracker.AddEvent("thing");
        var count = tracker.CountEvents("thing2", TimeSpan.FromSeconds(10));

        Assert.Equal(0, count);
    }

    [Fact]
    public void AddEvent_Many()
    {
        const int eventCount = 10;
        for(var i = 0; i < eventCount; i++)
            tracker.AddEvent("thing");

        var count = tracker.CountEvents("thing", TimeSpan.FromSeconds(10));

        Assert.Equal(eventCount, count);
    }
    
    [Fact]
    public void AddEvent_Many_NoTime()
    {
        const int eventCount = 10;
        for(var i = 0; i < eventCount; i++)
            tracker.AddEvent("thing");

        var count = tracker.CountEvents("thing", TimeSpan.Zero);

        Assert.Equal(0, count);
    }

    [Fact]
    public void AddEvent_ManyDifferent()
    {
        //These need to be divisible
        const int eventCount = 10;
        const int eventsPer = 2;

        for(var i = 0; i < eventCount; i++)
            tracker.AddEvent($"thing_{i / eventsPer}");

        for(var i = 0; i < eventCount; i++)
        {
            var count = tracker.CountEvents($"thing_{i / eventsPer}", TimeSpan.FromSeconds(10));
            Assert.Equal(eventsPer, count);
        }
    }

    [Fact]
    public void AddEvent_Many_InstantExpire()
    {
        const int eventCount = 10;
        config.MaximumKeep = TimeSpan.Zero;

        for(var i = 0; i < eventCount; i++)
            tracker.AddEvent("thing");

        var count = tracker.CountEvents("thing", TimeSpan.FromSeconds(10));

        //We want our system to be configurable enough to allow events to 
        //immediately, magically disappear
        Assert.Equal(0, count);
    }

    [Fact]
    public void AddEvent_Many_FastExpire()
    {
        const int eventCount = 10;
        config.MaximumKeep = TimeSpan.FromTicks(1);

        for(var i = 0; i < eventCount; i++)
            tracker.AddEvent("thing");

        var count = tracker.CountEvents("thing", TimeSpan.FromSeconds(10));

        //We want our system to be configurable enough to allow events to 
        //persist at least once if the timepsan is non-zero, even if super small
        Assert.Equal(1, count );
    }
}