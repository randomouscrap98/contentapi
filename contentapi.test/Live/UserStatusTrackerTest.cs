using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Live;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;


public class UserStatusTrackerTest : UnitTestBase
{
    protected UserStatusTracker service;

    public UserStatusTrackerTest()
    {
        service = new UserStatusTracker(GetService<ILogger<UserStatusTracker>>());
    }

    [Fact]
    public async Task GetAllStatusesAsync_EmptyOk()
    {
        var result = await service.GetUserStatusesAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetStatusForContentAsync_NoExistOk()
    {
        var result = await service.GetStatusForContentAsync(999);
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveStatusesByTrackerAsync_NoneOk()
    {
        //Just make sure it doesn't throw?
        var result = await service.RemoveStatusesByTrackerAsync(999);
        Assert.Empty(result); //.Equal(0, result);
    }

    [Fact]
    public async Task AddStatusAsync_SimpleOK()
    {
        await service.AddStatusAsync(1, 2, "active", 1);
        var result = await service.GetStatusForContentAsync(2);
        Assert.Contains(1, result.Keys);
        Assert.Equal("active", result[1]);
    }

    [Fact]
    public async Task AddStatusAsync_NullOrEmptyNotAdded()
    {
        await service.AddStatusAsync(1, 2, "", 1);
        var result = await service.GetUserStatusesAsync(2);
        Assert.Contains(2, result.Keys);
        Assert.Empty(result[2]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task AddStatusAsync_NullOrEmptyRemoves(string? status)
    {
        await service.AddStatusAsync(1, 2, "here!", 1);
        var result = await service.GetUserStatusesAsync(2);
        Assert.Contains(2, result.Keys);
        Assert.Equal("here!", result[2][1]);
        await service.AddStatusAsync(1, 2, status, 1);
        result = await service.GetUserStatusesAsync(2);
        Assert.Empty(result[2]);
    }

    [Fact]
    public async Task AddStatusAsync_GetAll()
    {
        await service.AddStatusAsync(1, 2, "active", 1);
        await service.AddStatusAsync(1, 3, "inactive", 1);
        var result = await service.GetUserStatusesAsync();
        Assert.Contains(2, result.Keys);
        Assert.Contains(3, result.Keys);
        Assert.Equal("active", result[2][1]);
        Assert.Equal("inactive", result[3][1]);
    }

    [Fact]
    public async Task AddStatusAsync_OverwriteSameTracker()
    {
        await service.AddStatusAsync(1, 2, "active", 1);
        await service.AddStatusAsync(1, 2, "inactive", 1);
        var result = await service.GetStatusForContentAsync(2);
        Assert.Equal("inactive", result[1]);
    }

    [Fact]
    public async Task AddStatusAsync_OverwriteDifferentTracker()
    {
        await service.AddStatusAsync(1, 2, "active", 1);
        await service.AddStatusAsync(1, 2, "inactive", 15);
        var result = await service.GetStatusForContentAsync(2);
        Assert.Equal("inactive", result[1]);
    }

    //NOTE: this ALSO tests whether empty status sets are not returned!
    [Fact]
    public async Task RemoveStatusesByTrackerAsync_SingleTracker()
    {
        await service.AddStatusAsync(1, 2, "active", 15);
        await service.AddStatusAsync(1, 3, "inactive", 15);
        var result = await service.GetUserStatusesAsync();
        Assert.Equal(2, result.Count);
        var removed = await service.RemoveStatusesByTrackerAsync(15);
        Assert.Equal(2, removed.Count);
        Assert.Equal(1, removed[2]);
        Assert.Equal(1, removed[3]);
        result = await service.GetUserStatusesAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveStatusesByTrackerAsync_DifferentTracker()
    {
        await service.AddStatusAsync(1, 2, "active", 15);
        await service.AddStatusAsync(1, 2, "inactive", 16);
        var result = await service.GetStatusForContentAsync(2);
        Assert.Equal("inactive", result[1]);
        var removed = await service.RemoveStatusesByTrackerAsync(16);
        Assert.Single(removed);
        Assert.Equal(1, removed[2]);
        result = await service.GetStatusForContentAsync(2);
        Assert.Equal("active", result[1]); //With the later one removed, status goes back
    }

    [Fact]
    public async Task GetUserStatusesAsync_SelectiveContent()
    {
        await service.AddStatusAsync(1, 2, "active", 1);
        await service.AddStatusAsync(1, 3, "inactive", 1);
        var result = await service.GetUserStatusesAsync(2);
        Assert.Single(result);
        Assert.Equal("active", result[2][1]);
        result = await service.GetUserStatusesAsync(3);
        Assert.Single(result);
        Assert.Equal("inactive", result[3][1]);
        result = await service.GetUserStatusesAsync(2,3,0);
        Assert.Equal(3, result.Count);
        Assert.Equal("active", result[2][1]);
        Assert.Equal("inactive", result[3][1]);
        Assert.Empty(result[0]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    public async Task SetUserStatus_ReportEach(int id1, int id2)
    {
        // Here, we must subscribe to the event 
        List<long> updates = new();
        service.StatusUpdated += (c) => { updates.Add(c); return Task.CompletedTask; };

        //We add two of different statuses from either the same or different tracker ids
        await service.AddStatusAsync(1, 2, "active", id1);
        await service.AddStatusAsync(1, 2, "inactive", id2);

        //Regardless, both statuses should've been updated
        Assert.Equal(2, updates.Count);
        Assert.All(updates, x => Assert.Equal(2, x));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    public async Task SetUserStatus_NoDoubleReport(int id1, int id2)
    {
        // Here, we must subscribe to the event 
        List<long> updates = new();
        service.StatusUpdated += (c) => { updates.Add(c); return Task.CompletedTask; };

        //We add two of the same status from either the same or different tracker ids
        await service.AddStatusAsync(1, 2, "active", id1);
        await service.AddStatusAsync(1, 2, "active", id2);

        //Regardless, there should've only been 1 update
        Assert.Single(updates);
        Assert.Equal(2, updates.First());
    }

    [Fact]
    public async Task RemoveStatusesByTrackerId_AllSimple()
    {
        List<long> contentIds = new List<long> { 2, 3, 5, 7 };

        foreach(var cid in contentIds)
            await service.AddStatusAsync(1, cid, "active", 99);
        
        //Then, with everything added, listen for contentId updates when the tracker is removed
        List<long> updates = new();
        service.StatusUpdated += (c) => { updates.Add(c); return Task.CompletedTask; };

        await service.RemoveStatusesByTrackerAsync(99);
        Assert.Equal(contentIds.ToHashSet(), updates.ToHashSet());
        Assert.Equal(contentIds.Count, updates.Count);

        updates = new();

        //Oh also, if you remove them again, nothing should be reported
        await service.RemoveStatusesByTrackerAsync(99);
        Assert.Empty(updates);
    }

    [Fact]
    public async Task RemoveStatusesByTrackerId_PartialSimple()
    {
        List<long> contentIds = new List<long> { 2, 3, 5, 7 };
        List<long> contentIds2 = new List<long> { 2, 3, 11, 13 };

        foreach(var cid in contentIds)
            await service.AddStatusAsync(1, cid, "active", 99);

        //Add a DIFFERENT user id under a different tracker to a different but overlapping set of content
        foreach(var cid in contentIds2)
            await service.AddStatusAsync(2, cid, "active", 100);
        
        //Then, with everything added, listen for contentId updates when the tracker is removed
        List<long> updates = new();
        service.StatusUpdated += (c) => { updates.Add(c); return Task.CompletedTask; };

        //Regardless, only the ORIGINAL set of contents should be reported.
        await service.RemoveStatusesByTrackerAsync(99);
        Assert.Equal(contentIds.ToHashSet(), updates.ToHashSet());

        updates = new();

        //And then removing the other user should report the other set
        await service.RemoveStatusesByTrackerAsync(100);
        Assert.Equal(contentIds2.ToHashSet(), updates.ToHashSet());
    }
}