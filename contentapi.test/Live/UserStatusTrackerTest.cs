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
        Assert.Equal(2, result.Count);
        Assert.Equal("active", result[2][1]);
        Assert.Equal("inactive", result[3][1]);
    }
}