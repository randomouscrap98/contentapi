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
        var result = await service.GetAllStatusesAsync();
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
        Assert.Equal(0, result);
    }
}