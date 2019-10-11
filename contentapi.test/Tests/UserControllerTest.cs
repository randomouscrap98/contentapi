using contentapi.Controllers;
using Xunit;
using contentapi.Models;
using System;
using contentapi.Services;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.test
{
    public class UserControllerTest : ControllerTestBase<UsersController>
    {
        public UserControllerTest(TestControllerContext context) : base(context) {}

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestBasicUserCreate(bool loggedIn)
        {
            context.SetLoginState(loggedIn);
            var credential = context.GetNewCredentials();
            var userResult = controller.PostCredentials(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            Assert.True(userResult.Value.id > 0);
            Assert.True(userResult.Value.createDate <= DateTime.Now);
            Assert.True(userResult.Value.createDate > DateTime.Now.AddDays(-1));
        }

        private void TestUserCreateDupe(Action<UserCredential> alterCredential)
        {
            var credential = context.GetNewCredentials();
            var userResult = controller.PostCredentials(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            alterCredential(credential);
            userResult = controller.PostCredentials(credential).Result;
            Assert.True(context.IsBadRequest(userResult.Result));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserCreateDupeUsername(bool loggedIn)
        {
            context.SetLoginState(loggedIn);
            TestUserCreateDupe((c) => c.email += "a");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserCreateDupeEmail(bool loggedIn)
        {
            context.SetLoginState(loggedIn);
            TestUserCreateDupe((c) => c.username += "a");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserAuthenticate(bool loggedIn)
        {
            context.SetLoginState(loggedIn);
            var result = controller.Authenticate(context.SessionCredentials).Result;
            Assert.True(context.IsSuccessRequest(result));
        }

        [Fact]
        public void TestUserMe()
        {
            context.Login();
            var result = controller.Me().Result;
            Assert.Equal(context.SessionResult.id, result.Value.id);
            Assert.Equal(context.SessionResult.username, result.Value.username);
        }

        [Fact]
        public void TestUserMeLogout()
        {
            context.Logout();
            var result = controller.Me().Result;
            Assert.True(context.IsBadRequest(result.Result) || context.IsNotFound(result.Result)); //This may not always be a bad request!
            //Assert.True(context.IsNotAuthorized(result.Result));
            Assert.Null(result.Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserSelfDeleteFail(bool loggedIn)
        {
            //I don't care WHO we are, we can't delete!
            context.SetLoginState(loggedIn);
            var result = controller.Delete(context.SessionResult.id).Result;
            Assert.False(context.IsSuccessRequest(result));
        }

        [Fact]
        public void TestRandomDeleteFail()
        {
            context.Login();
            var creds = context.GetNewCredentials();
            var newUser = controller.PostCredentials(creds).Result;
            Assert.True(newUser.Value.id > 0); //Just make sure a new user was created
            var result = controller.Delete(newUser.Value.id).Result;
            Assert.False(context.IsSuccessRequest(result));
        }

        [Fact]
        public void TestGetUsers()
        {
            var result = controller.Get(new CollectionQuery()).Result;
            Assert.True(context.IsSuccessRequest(result));
            List<UserView> users = ((IEnumerable<UserView>)result.Value["collection"]).ToList();
            Assert.True(users.Count > 0);
            Assert.Contains(users, x => x.id == context.SessionResult.id);
        }

        [Fact]
        public void TestGetUserSingle()
        {
            var result = controller.GetSingle(context.SessionResult.id).Result;
            Assert.True(context.IsSuccessRequest(result));
            Assert.True(result.Value.id == context.SessionResult.id);
        }
    }
}