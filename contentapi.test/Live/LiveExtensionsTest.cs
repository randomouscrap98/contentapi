using System.Linq;
using System.Threading.Tasks;
using contentapi.Db;
using contentapi.Live;
using contentapi.Search;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class LiveExtensionsTest : ViewUnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected IUserStatusTracker userStatuses;
    protected IGenericSearch searcher;
    protected DbUnitTestSearchFixture fixture;

    public LiveExtensionsTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        userStatuses = fixture.GetService<IUserStatusTracker>();
        searcher = fixture.GetService<IGenericSearch>();
    }

    [Fact]
    public async Task GetAllStatusesAsync_0Exists()
    {
        await userStatuses.AddStatusAsync(SuperUserId, 0, "here!", 1);
        var result = await userStatuses.GetAllStatusesAsync(searcher, NormalUserId);
        Assert.Contains(0, result.statuses.Keys);
        Assert.Contains("user", result.data.Keys);
        Assert.Contains("content", result.data.Keys);
        Assert.Empty(result.data["content"]);
        var users = searcher.ToStronglyTyped<UserView>(result.data["user"]);
        Assert.Single(users);
        Assert.Equal(SuperUserId, users.First().id);
    }
}