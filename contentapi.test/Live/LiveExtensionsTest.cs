using System.Linq;
using System.Threading.Tasks;
using contentapi.Live;
using contentapi.Search;
using contentapi.data.Views;
using Xunit;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class LiveExtensionsTest : ViewUnitTestBase //, IClassFixture<DbUnitTestSearchFixture>
{
    protected IUserStatusTracker userStatuses;
    protected IGenericSearch searcher;
    protected DbUnitTestSearchFixture fixture;

    public const string EasyFields = "id,name,createUserId";

    public LiveExtensionsTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        userStatuses = GetService<IUserStatusTracker>(); //DON'T use the fixture! want NEW service every time!
        searcher = fixture.GetService<IGenericSearch>();
        fixture.ResetDatabase();
    }

    [Fact]
    public async Task GetAllStatusesAsync_0Exists()
    {
        await userStatuses.AddStatusAsync(SuperUserId, 0, "here!", 1);
        var result = await userStatuses.GetUserStatusesAsync(searcher, NormalUserId, EasyFields);
        Assert.Contains(0, result.statuses.Keys);
        Assert.Contains("user", result.objects.Keys);
        Assert.Contains("content", result.objects.Keys);
        Assert.Empty(result.objects["content"]);
        var users = searcher.ToStronglyTyped<UserView>(result.objects["user"]);
        Assert.Single(users);
        Assert.Equal(SuperUserId, users.First().id);
    }

    [Fact]
    public async Task GetAllStatusesAsync_NoSecret()
    {
        await userStatuses.AddStatusAsync(SuperUserId, SuperAccessContentId, "here!", 1);
        var result = await userStatuses.GetUserStatusesAsync(searcher, NormalUserId, EasyFields);
        Assert.Empty(result.statuses);
        Assert.Contains("user", result.objects.Keys);
        Assert.Contains("content", result.objects.Keys);
        Assert.Empty(result.objects["content"]);
        Assert.Empty(result.objects["user"]);
    }

    [Fact]
    public async Task GetAllStatusesAsync_SingleNormal()
    {
        await userStatuses.AddStatusAsync(SuperUserId, AllAccessContentId, "here!", 1);
        var result = await userStatuses.GetUserStatusesAsync(searcher, NormalUserId, EasyFields);
        Assert.Single(result.statuses);
        Assert.Contains(AllAccessContentId, result.statuses.Keys);
        Assert.Single(result.statuses[AllAccessContentId]);
        Assert.Equal("here!", result.statuses[AllAccessContentId][SuperUserId]);

        Assert.Contains("user", result.objects.Keys);
        Assert.Contains("content", result.objects.Keys);
        Assert.Contains(result.objects["content"], x => (long)x["id"] == AllAccessContentId);
        Assert.Contains(result.objects["user"], x => (long)x["id"] == SuperUserId);
    }

    [Fact]
    public async Task GetAllStatusesAsync_Complex()
    {
        await userStatuses.AddStatusAsync(SuperUserId, AllAccessContentId, "here!", 1);
        await userStatuses.AddStatusAsync(NormalUserId, 0, "also here!", 2);
        await userStatuses.AddStatusAsync(SuperUserId, SuperAccessContentId, "secret here!", 1);
        
        //The normal user will have access to 0 and the all access content, but not the super content
        var result = await userStatuses.GetUserStatusesAsync(searcher, NormalUserId, EasyFields, "*", 0, AllAccessContentId, SuperAccessContentId);
        Assert.Equal(2, result.statuses.Count);
        Assert.Contains(AllAccessContentId, result.statuses.Keys);
        Assert.Contains(0, result.statuses.Keys);
        Assert.Single(result.statuses[AllAccessContentId]);
        Assert.Equal("here!", result.statuses[AllAccessContentId][SuperUserId]);
        Assert.Single(result.statuses[0]);
        Assert.Equal("also here!", result.statuses[0][NormalUserId]);

        Assert.Contains("user", result.objects.Keys);
        Assert.Contains("content", result.objects.Keys);
        Assert.Contains(result.objects["content"], x => (long)x["id"] == AllAccessContentId);
        Assert.DoesNotContain(result.objects["content"], x => (long)x["id"] == 0);
        Assert.Contains(result.objects["user"], x => (long)x["id"] == SuperUserId);
        Assert.Contains(result.objects["user"], x => (long)x["id"] == NormalUserId);
    }
}