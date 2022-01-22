using System;
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
            MinUsernameLength = 4,
            MaxUsernameLength = 8,
            UsernameRegex = "^[a-zA-Z0-9]+$"
        };

        searcher = fixture.GetService<IGenericSearch>();
        this.fixture = fixture;

        this.service = new UserService(fixture.GetService<ILogger<UserService>>(), searcher, fixture.GetService<IHashService>(), 
            fixture.GetService<IAuthTokenService<long>>(), config, fixture.GetService<ContentApiDbConnection>());

        //Always want a fresh database!
        fixture.ResetDatabase();
    }

    [Fact]
    public async Task CreateNewUser_Basic()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        AssertDateClose(user.createDate);
        Assert.True(user.id > 0);
        Assert.False(user.registered);
        Assert.Equal("hello", user.username);
        Assert.Equal(UserType.user, user.type);
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
    public async Task CreateNewUser_GetRegistration()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        var registration = await service.GetRegistrationKeyAsync(user.id);
        Assert.Equal(service.RegistrationLog[user.id], registration);
        //Retrieving the registration shouldn't REGISTER them
        var completedUser = await searcher.GetById<UserView>(RequestType.user, user.id);
        Assert.False(completedUser.registered);
    }

    [Fact]
    public async Task CreateNewUser_Register()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(user.id, service.RegistrationLog[user.id]);
        Assert.False(string.IsNullOrWhiteSpace(token));
        var completedUser = await searcher.GetById<UserView>(RequestType.user, user.id);
        Assert.True(completedUser.registered);
    }

    [Fact]
    public async Task CreateNewUser_Register_UnknownUser()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var token = await service.CompleteRegistration(99, service.RegistrationLog[user.id]);
        });
    }

    [Fact]
    public async Task CreateNewUser_Register_BadRegistrationKey()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<RequestException>(async () => {
            var token = await service.CompleteRegistration(user.id, service.RegistrationLog[user.id] + "A");
        });
    }

    [Fact]
    public async Task CreateNewUser_LoginUsername()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(user.id, service.RegistrationLog[user.id]);
        var loginToken = await service.LoginUsernameAsync("hello", "short");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task CreateNewUser_LoginEmail()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(user.id, service.RegistrationLog[user.id]);
        var loginToken = await service.LoginEmailAsync("email@email.com", "short");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task CreateNewUser_Login_BadPassword()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");
        var token = await service.CompleteRegistration(user.id, service.RegistrationLog[user.id]);
        await Assert.ThrowsAnyAsync<RequestException>(async () => {
            var loginToken = await service.LoginUsernameAsync("hello", "shorts");
        });
    }

    [Fact]
    public async Task CreateNewUser_UsernameCollision()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var user2 = await service.CreateNewUser("hello", "short2", "email2@email.com");
        });
    }

    [Fact]
    public async Task CreateNewUser_EmailCollision()
    {
        var user = await service.CreateNewUser("hello", "short", "email@email.com");

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var user2 = await service.CreateNewUser("hello2", "short2", "email@email.com");
        });
    }

    [Theory]
    [InlineData("ab", "short")]  //Username too short
    [InlineData("abcdefgt9", "short")] //Username too long
    [InlineData("hello", "sho")] //Password too short
    [InlineData("hello", "shortlong")] //Password too long
    [InlineData("!hello", "short")] //bad char in username
    public async Task CreateNewUser_InputErrors(string username, string password)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var user = await service.CreateNewUser(username, password, "email@email.com");
        });
    }
}