using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class CheckpointTracketTest : UnitTestBase
{
    protected CheckpointTracker tracker;

    public CheckpointTracketTest()
    {
        tracker = new CheckpointTracker(GetService<ILogger<CheckpointTracker>>());
    }

    [Fact]
    public void WaitForCheckpoint_EmptyNoWait()
    {
        var result = tracker.WaitForCheckpoint("something", -1, safetySource.Token).Result;
        Assert.Equal(0, result);
    }

    [Fact]
    public void UpdateCheckpoint_NoWaiters()
    {
        var result = tracker.UpdateCheckpoint("whatever");
        Assert.Equal(1, result);
    }

    [Fact]
    public void UpdateCheckpoint_IndividualTracking()
    {
        var result = tracker.UpdateCheckpoint("whatever");
        Assert.Equal(1, result);
        result = tracker.UpdateCheckpoint("thing");
        Assert.Equal(1, result);
        result = tracker.UpdateCheckpoint("whatever");
        Assert.Equal(2, result);
        result = tracker.UpdateCheckpoint("things");
        Assert.Equal(1, result);
    }

    [Fact]
    public void WaitForCheckpoint_MultipleWaiters()
    {
        List<Task<int>> waiters = new List<Task<int>>();
        const int num = 3;

        for(int i = 0; i < num; i++)
            waiters.Add(tracker.WaitForCheckpoint("something", 0, safetySource.Token));

        //Nobody should be finished
        for(int i = 0; i < num; i++)
            Assert.False(waiters[i].IsCanceled || waiters[i].IsCompleted);

        //Now do the update
        tracker.UpdateCheckpoint("something");

        //All should get 1
        for(int i = 0; i < num; i++)
        {
            var result = waiters[i].Result;
            Assert.Equal(1, result);
        }
    }

    [Fact]
    public void WaitForCheckpoint_NoCrossSignal()
    {
        var waiter1 = tracker.WaitForCheckpoint("something", 0, safetySource.Token);
        var waiter2 = tracker.WaitForCheckpoint("somethingElse", 0, safetySource.Token);

        //Now do the update only for nobody
        tracker.UpdateCheckpoint("nobody");

        //They shouldn't complete
        Assert.False(waiter1.IsCanceled || waiter1.IsCompleted);
        Assert.False(waiter2.IsCanceled || waiter2.IsCompleted);

        //Now do the update only for waiter2
        tracker.UpdateCheckpoint("somethingElse");

        var result = waiter2.Result;
        Assert.Equal(1, result);
        Assert.False(waiter1.IsCanceled || waiter1.IsCompleted);

        //And then do the update for waiter1
        tracker.UpdateCheckpoint("something");

        result = waiter1.Result;
        Assert.Equal(1, result);
    }
}