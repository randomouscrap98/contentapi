using System;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Xunit;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class CombinedUserTests : ViewUnitTestBase //, IClassFixture<DbUnitTestSearchFixture>
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

        //Always want a fresh database!
        fixture.ResetDatabase();
    }

    [Fact]
    public async Task DeletedUserNoLogin()
    {
        var userId = await service.CreateNewUser("hello", Password, "email@email.com");
        var token = await service.CompleteRegistration(userId, await service.GetRegistrationKeyAsync(userId));
        var loginToken = await service.LoginUsernameAsync("hello", Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
        //Login worked, now delete them
        var deleteResult = await writer.DeleteAsync<UserView>(userId, (int)UserVariations.Super + 1);
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
        var userId = await service.CreateNewUser("hello", Password, "email@email.com");
        var token = await service.CompleteRegistration(userId, await service.GetRegistrationKeyAsync(userId));
        var loginToken = await service.LoginUsernameAsync("hello", Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
        var user = await searcher.GetById<UserView>(userId);
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
        var userId = await service.CreateNewUser("hello", Password, "email@email.com");
        var token = await service.CompleteRegistration(userId, await service.GetRegistrationKeyAsync(userId));
        var loginToken = await service.LoginUsernameAsync("hello", Password);
        var user = await searcher.GetById<UserView>(userId);
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
        var userId = await service.CreateNewUser("hello", Password, "email@email.com");
        var regToken = await service.GetRegistrationKeyAsync(userId);
        var user = await searcher.GetById<UserView>(userId);

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

    [Fact]
    public async Task UpdatedUserUnmodifiedPrivates()
    {
        //Assume this worked
        var userId = await service.CreateNewUser("hello", Password, "email@email.com");
        var token = await service.CompleteRegistration(userId, await service.GetRegistrationKeyAsync(userId));
        var user = await searcher.GetById<UserView>(userId);

        //With no hidelist, try to modify other data
        user.special = "seomthingNEWW";
        var updateResult = await writer.WriteAsync(user, user.id);

        //Then get the user's private data. it should all be fine.
        var privateData = await service.GetPrivateData(user.id);
        Assert.Equal("email@email.com", privateData.email);
        //Assert.Empty(privateData.hideList);

        //Now set the email 
        //var newHidelist = new List<long> {5, 10} ;
        await service.SetPrivateData(user.id, new UserSetPrivateData() { email = "somethingelse@email.com" });

        //And then update the user AGAIN
        user.special = "amazing";
        updateResult = await writer.WriteAsync(user, user.id);

        //Finally, make sure the private data is fine.
        privateData = await service.GetPrivateData(user.id);
        Assert.Equal("somethingelse@email.com", privateData.email);
        //Assert.True(newHidelist.SequenceEqual(privateData.hideList!));
    }

    [Theory]
    [InlineData((int)UserVariations.Super + 1, false)]
    [InlineData((int)UserVariations.Super + 1, true)]
    public async Task NoGroupLogin(long writerId, bool super)
    {
        //Go add a group real quick
        var group = new UserView()
        {
            username = "whatever_dude",
            type = Db.UserType.group,
            super = super
        };

        var result = await writer.WriteAsync(group, writerId);
        Assert.Equal(group.username, result.username);

        //Make sure NONE OF THESE succeed

        //Should be some kind of forbidden exception for clarity: groups can't login
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => service.LoginUsernameAsync(result.username, ""));
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => service.VerifyPasswordAsync(result.id, ""));
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => service.LoginUsernameAsync(result.username, "abc"));
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => service.VerifyPasswordAsync(result.id, "abc"));
    }
}