
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.test.Mock;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class CacheCheckpointTrackerTest : UnitTestBase
{
    protected CacheCheckpointTracker<int> tracker;
    protected CacheCheckpointTracker<SimpleLinkedCheckpointId> trackerIds;
    protected CacheCheckpointTrackerConfig config;

    public CacheCheckpointTrackerTest()
    {
        config = new CacheCheckpointTrackerConfig();
        tracker = new CacheCheckpointTracker<int>(GetService<ILogger<CacheCheckpointTracker<int>>>(), config);
        trackerIds = new CacheCheckpointTracker<SimpleLinkedCheckpointId>(GetService<ILogger<CacheCheckpointTracker<SimpleLinkedCheckpointId>>>(), config);
    }

    [Fact]
    public void MaximumCacheChecpoint_Empty()
    {
        var result = tracker.MaximumCacheCheckpoint("something"); //tracker.WaitForCheckpoint("something", -1, safetySource.Token);
        Assert.Equal(0, result); //result.LastId);
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
        List<Task<CacheCheckpointResult<int>>> waiters = new List<Task<CacheCheckpointResult<int>>>();
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
        List<Task<CacheCheckpointResult<int>>> waiters = new List<Task<CacheCheckpointResult<int>>>();
        const int num = 3;

        for(int i = 0; i < num; i++)
            waiters.Add(tracker.WaitForCheckpoint("something", 0, safetySource.Token));

        //Nobody should be finished
        for(int i = 0; i < num; i++)
            Assert.False(waiters[i].IsCanceled || waiters[i].IsCompleted);

        tracker.UpdateCheckpoint("something", 89);
        tracker.UpdateCheckpoint("something", -5);

        //All should get the correct results
        for(int i = 0; i < num; i++)
        {
            var result = await waiters[i];
            Assert.Equal(2, result.LastId);
            Assert.Equal(2, result.Data.Count());
            Assert.Equal(89, result.Data[0]);
            Assert.Equal(-5, result.Data[1]);
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

    [Fact]
    public async Task WaitForCheckpoint_LinkedId()
    {
        var id = trackerIds.UpdateCheckpoint("simple", new SimpleLinkedCheckpointId());
        var waiter = await trackerIds.WaitForCheckpoint("simple", 0, safetySource.Token);
        Assert.Equal(id, waiter.LastId);
        Assert.Single(waiter.Data);
        Assert.Equal(id, waiter.Data.First().id);
    }

    [Fact]
    public void UpdateCheckpoint_Stepping()
    {
        config.CacheIdIncrement = 10;
        var checkpointObject = new SimpleLinkedCheckpointId();
        var id = trackerIds.UpdateCheckpoint("simple", checkpointObject);
        Assert.Equal(10, id);
        Assert.Equal(10, checkpointObject.id);
        var checkpoint2 = new SimpleLinkedCheckpointId();
        id = trackerIds.UpdateCheckpoint("simple", checkpoint2);
        Assert.Equal(20, id);
        Assert.Equal(20, checkpoint2.id);
    }

    [Fact]
    public void UpdateCheckpoint_SessionBase()
    {
        config.CacheIdIncrement = 10;
        trackerIds.UniqueSessionId = 3;
        var checkpointObject = new SimpleLinkedCheckpointId();
        var id = trackerIds.UpdateCheckpoint("simple", checkpointObject);
        Assert.Equal(13, id);
        Assert.Equal(13, checkpointObject.id);
        var checkpoint2 = new SimpleLinkedCheckpointId();
        id = trackerIds.UpdateCheckpoint("simple", checkpoint2);
        Assert.Equal(23, id);
        Assert.Equal(23, checkpoint2.id);
    }

    [Fact]
    public async Task UpdateCheckpoint_BadSession()
    {
        config.CacheIdIncrement = 10;
        trackerIds.UniqueSessionId = 3;
        var id = trackerIds.UpdateCheckpoint("simple", new SimpleLinkedCheckpointId());
        var checkpointObject = new SimpleLinkedCheckpointId();
        id = trackerIds.UpdateCheckpoint("simple", checkpointObject);
        await Assert.ThrowsAnyAsync<ExpiredCheckpointException>(() => trackerIds.WaitForCheckpoint("simple", 14, safetySource.Token));
        await Assert.ThrowsAnyAsync<ExpiredCheckpointException>(() => trackerIds.WaitForCheckpoint("simple", 10, safetySource.Token));
        await Assert.ThrowsAnyAsync<ExpiredCheckpointException>(() => trackerIds.WaitForCheckpoint("simple", 6, safetySource.Token));
        var result = await trackerIds.WaitForCheckpoint("simple", 13, safetySource.Token);
        Assert.Equal(23, result.LastId);
        Assert.Single(result.Data);
        Assert.Equal(23, result.Data.First().id);
    }

    [Fact]
    public void UniqueSessionDefaultZero()
    {
        Assert.Equal(0, trackerIds.UniqueSessionId);
    }
}