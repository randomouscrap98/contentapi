using System;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.Views;
using Xunit;

namespace contentapi.test;

public class CombinedUserTests : ViewUnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected IDbWriter writer;
    protected IGenericSearch searcher;
    protected IUserService service;

    protected const string Password = "thisIsAPassword98098";

    public CombinedUserTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();

        searcher = fixture.GetService<IGenericSearch>();
        writer = fixture.GetService<IDbWriter>();
        service = fixture.GetService<IUserService>();

        //UserService(fixture.GetService<ILogger<UserService>>(), searcher, fixture.GetService<IHashService>(), 
        //    fixture.GetService<IAuthTokenService<long>>(), config, fixture.GetService<ContentApiDbConnection>());

        //Always want a fresh database!
        fixture.ResetDatabase();
    }

    [Fact]
    public async Task DeletedUserNoLogin()
    {
        var user = await service.CreateNewUser("hello", Password, "email@email.com");
        var token = await service.CompleteRegistration(user.id, await service.GetRegistrationKeyAsync(user.id));
        var loginToken = await service.LoginUsernameAsync("hello", Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
        //Login worked, now delete them
        var deleteResult = await writer.DeleteAsync<UserView>(user.id, (int)UserVariations.Super + 1);
        //Login should no longer work for them
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
        {
            var loginToken = await service.LoginUsernameAsync("hello", Password);
            Assert.Empty(loginToken);
        });
        //Also, login shouldn't work for whatever NEW username is there
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
        {
            var loginToken = await service.LoginUsernameAsync(deleteResult.username, Password);
            Assert.Empty(loginToken);
        });
    }

    [Fact]
    public async Task UpdatedUserYesLogin()
    {
        var user = await service.CreateNewUser("hello", Password, "email@email.com");
        var token = await service.CompleteRegistration(user.id, await service.GetRegistrationKeyAsync(user.id));
        var loginToken = await service.LoginUsernameAsync("hello", Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
        //Login worked, now update the user's special field or something and ensure nothing exploded.
        user.special = "seomthingNEWW";
        var updateResult = await writer.WriteAsync(user, user.id);
        loginToken = await service.LoginUsernameAsync("hello", Password);
        Assert.False(string.IsNullOrWhiteSpace(loginToken)); //login should still work even though they changed something about them
        //You should also still be able to login with your email
        loginToken = await service.LoginEmailAsync("email@email.com", Password);
        Assert.False(string.IsNullOrWhiteSpace(loginToken)); //login should still work even though they changed something about them
    }

    [Fact] //WARN: This may eventually STOP working if username changes... well, change
    public async Task UpdatedUserUsernameYesLogin()
    {
        var user = await service.CreateNewUser("hello", Password, "email@email.com");
        var token = await service.CompleteRegistration(user.id, await service.GetRegistrationKeyAsync(user.id));
        var loginToken = await service.LoginUsernameAsync("hello", Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
        //Login worked, now update the user's special field or something and ensure nothing exploded.
        user.username = "ABRANDNEWNUG";
        var updateResult = await writer.WriteAsync(user, user.id);
        loginToken = await service.LoginUsernameAsync("ABRANDNEWNUG", Password);
        Assert.False(string.IsNullOrWhiteSpace(loginToken)); //login should still work even though they changed something about them
        //Login should no longer work for the old username
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
        {
            var loginToken = await service.LoginUsernameAsync("hello", Password);
            Assert.Empty(loginToken);
        });
    }

    [Fact] //Updating a user in the middle of registration (for some reason) should not break their registration
    public async Task UpdatedUserYesRegister()
    {
        var user = await service.CreateNewUser("hello", Password, "email@email.com");
        var regToken = await service.GetRegistrationKeyAsync(user.id);

        //Wow, updating user in the MIDDLE of registration, how could you?
        user.special = "seomthingNEWW";
        var updateResult = await writer.WriteAsync(user, user.id);

        //Make sure the token that we had before still works now
        var newToken = await service.GetRegistrationKeyAsync(user.id);
        Assert.Equal(regToken, newToken);

        //OK complete registration AFTER an update and we should be all good!
        var token = await service.CompleteRegistration(user.id, newToken);
        var loginToken = await service.LoginUsernameAsync("hello", Password);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
    }
}