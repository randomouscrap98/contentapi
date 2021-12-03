
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class CacheCheckpointTrackerTest : UnitTestBase
{
    protected CacheCheckpointTracker tracker;
    protected CacheCheckpointTrackerConfig config;
    protected CancellationTokenSource cancelSource;
    protected CancellationTokenSource safetySource;

    public CacheCheckpointTrackerTest()
    {
        config = new CacheCheckpointTrackerConfig();
        tracker = new CacheCheckpointTracker(GetService<ILogger<CacheCheckpointTracker>>(), config);
        cancelSource = new CancellationTokenSource();
        safetySource = new CancellationTokenSource();
        safetySource.CancelAfter(5000);
    }

    [Fact]
    public async Task WaitForCheckpoint_EmptyNoWait()
    {
        var result = await tracker.WaitForCheckpoint("something", -1, safetySource.Token);
        Assert.Equal(0, result.LastId);
    }

    [Fact]
    public void UpdateCheckpoint_NoWaiters()
    {
        var result = tracker.UpdateCheckpoint("whatever", 5);
        Assert.Equal(1, result);
    }

    [Fact]
    public void UpdateCheckpoint_IndividualTracking()
    {
        var result = tracker.UpdateCheckpoint("whatever", 5);
        Assert.Equal(1, result);
        result = tracker.UpdateCheckpoint("thing", 69);
        Assert.Equal(1, result);
        result = tracker.UpdateCheckpoint("whatever", 6);
        Assert.Equal(2, result);
        result = tracker.UpdateCheckpoint("things", 420);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task WaitForCheckpoint_MultipleWaiters()
    {
        List<Task<CacheCheckpointResult>> waiters = new List<Task<CacheCheckpointResult>>();
        const int num = 3;

        for(int i = 0; i < num; i++)
            waiters.Add(tracker.WaitForCheckpoint("something", 0, safetySource.Token));

        //Nobody should be finished
        for(int i = 0; i < num; i++)
            Assert.False(waiters[i].IsCanceled || waiters[i].IsCompleted);

        //Now do the update
        tracker.UpdateCheckpoint("something", 89);

        //All should get 1 and 89
        for(int i = 0; i < num; i++)
        {
            var result = await waiters[i];
            Assert.Equal(1, result.LastId);
            Assert.Single(result.Data);
            Assert.Equal(89, result.Data.First());
        }
    }

    [Fact]
    public async Task WaitForCheckpoint_NoCrossSignal()
    {
        var waiter1 = tracker.WaitForCheckpoint("something", 0, safetySource.Token);
        var waiter2 = tracker.WaitForCheckpoint("somethingElse", 0, safetySource.Token);

        //Now do the update only for nobody
        tracker.UpdateCheckpoint("nobody", 999);

        //They shouldn't complete
        Assert.False(waiter1.IsCanceled || waiter1.IsCompleted);
        Assert.False(waiter2.IsCanceled || waiter2.IsCompleted);

        //Now do the update only for waiter2
        tracker.UpdateCheckpoint("somethingElse", 989);

        var result = await waiter2;
        Assert.Equal(1, result.LastId);
        Assert.Single(result.Data);
        Assert.Equal(989, result.Data.First());
        Assert.False(waiter1.IsCanceled || waiter1.IsCompleted);

        //And then do the update for waiter1
        tracker.UpdateCheckpoint("something", 777);

        result = await waiter1;
        Assert.Equal(1, result.LastId);
        Assert.Single(result.Data);
        Assert.Equal(777, result.Data.First());
    }

    [Fact]
    public async Task WaitForCheckpoint_MultipleResults()
    {
        //Also test multiple waiters because why not
        List<Task<CacheCheckpointResult>> waiters = new List<Task<CacheCheckpointResult>>();
        const int num = 3;

        for(int i = 0; i < num; i++)
            waiters.Add(tracker.WaitForCheckpoint("something", 0, safetySource.Token));

        //Nobody should be finished
        for(int i = 0; i < num; i++)
            Assert.False(waiters[i].IsCanceled || waiters[i].IsCompleted);

        //Now do the updates. Don't care if the types aren't the same, this is used only for user output.
        tracker.UpdateCheckpoint("something", 89);
        tracker.UpdateCheckpoint("something", true);

        //All should get the correct results
        for(int i = 0; i < num; i++)
        {
            var result = await waiters[i];
            Assert.Equal(2, result.LastId);
            Assert.Equal(2, result.Data.Count());
            Assert.Equal(89, result.Data[0]);
            Assert.Equal(true, result.Data[1]);
        }
    }

    [Fact]
    public async Task WaitForCheckpoint_CacheClear()
    {
        //This makes the cache clear every single time.
        config.CacheAge = TimeSpan.Zero;
        config.CacheCleanFrequency = 1;

        tracker.UpdateCheckpoint("something", 89);
        tracker.UpdateCheckpoint("something", 55); //This should be the ONLY thing in the cache at this point, with an ID of 2

        //This should succeed just fine, but only return one item
        var result = await tracker.WaitForCheckpoint("something", 0, safetySource.Token);

        //We inserted two items, so the id should be two
        Assert.Equal(2, result.LastId);
        Assert.Single(result.Data);
        Assert.Equal(55, result.Data.First());

    }

    [Fact]
    public async Task WaitForCheckpoint_ExpiredException()
    {
        //This makes the cache clear every single time.
        config.CacheAge = TimeSpan.Zero;
        config.CacheCleanFrequency = 1;

        tracker.UpdateCheckpoint("something", 89);
        tracker.UpdateCheckpoint("something", 55); //This should be the ONLY thing in the cache at this point, with an ID of 2

        await Assert.ThrowsAnyAsync<ExpiredCheckpointException>(async () => {
            //1 is out of date! It got cleared! Assuming the previous test passes, anyway (not that this test depends on that data,
            //it just means the cache clearing system is working)
            var result = await tracker.WaitForCheckpoint("something", 1, safetySource.Token);
        });
    }
}