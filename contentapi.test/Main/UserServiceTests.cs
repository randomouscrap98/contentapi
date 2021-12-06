using System.Threading.Tasks;
using contentapi.Db;
using contentapi.Main;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class UserServiceTests : UnitTestBase, IClassFixture<DbUnitTestBase>
{
    protected DbUnitTestBase fixture;
    protected UserService service;
    protected UserServiceConfig config;
    protected IGenericSearch searcher;

    public UserServiceTests(DbUnitTestBase fixture)
    {
        config = new UserServiceConfig {
            MinPasswordLength = 4,
            MaxPasswordLength = 8,
            UsernameRegex = "^[a-zA-Z0-9]+$"
        };

        searcher = fixture.GetService<IGenericSearch>();
        this.fixture = fixture;

        this.service = new UserService(fixture.GetService<ILogger<UserService>>(), searcher, fixture.GetService<IHashService>(), 
            fixture.GetService<IAuthTokenService<long>>(), config, fixture.GetService<ContentApiDbConnection>());
    }

    [Fact]
    public async Task CreateNewUser_Basic()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        AssertDateClose(user.createDate);
        Assert.True(user.id > 0);
        Assert.False(user.registered);
        Assert.Equal("hello", user.username);
    }

    [Fact]
    public async Task CreateNewUser_LoginBeforeRegisterFail()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        await Assert.ThrowsAnyAsync<ForbiddenException>(async () => {
            var token = await service.LoginUsernameAsync("hello", "short");
        });
        //Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task CreateNewUser_Register()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(user.id, service.RegistrationLog[user.id]);
        Assert.False(string.IsNullOrWhiteSpace(token));
        var completedUser = await searcher.GetById<UserView>(RequestType.user, user.id);
        Assert.True(user.registered);
    }
}