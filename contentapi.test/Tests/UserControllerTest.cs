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
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestBasicUserCreate(bool loggedIn)
        {
            var instance = GetInstance(loggedIn);
            var credential = GetNewCredentials();
            var userResult = instance.Controller.PostCredentials(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            Assert.True(userResult.Value.id > 0);
            Assert.True(userResult.Value.createDate <= DateTime.Now);
            Assert.True(userResult.Value.createDate > DateTime.Now.AddDays(-1));
        }

        private void TestUserCreateDupe(ControllerInstance<UsersController> instance, Action<UserCredential> alterCredential)
        {
            var credential = GetNewCredentials();
            var userResult = instance.Controller.PostCredentials(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            alterCredential(credential);
            userResult = instance.Controller.PostCredentials(credential).Result;
            Assert.True(IsBadRequest(userResult.Result));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserCreateDupeUsername(bool loggedIn)
        {
            TestUserCreateDupe(GetInstance(loggedIn), (c) => c.email += "a");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserCreateDupeEmail(bool loggedIn)
        {
            TestUserCreateDupe(GetInstance(loggedIn), (c) => c.username += "a");
        }

        [Fact]
        public void TestUserMe()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.Me().Result;
            Assert.Equal(instance.User.id, result.Value.id);
            Assert.Equal(instance.User.username, result.Value.username);
        }

        [Fact]
        public void TestUserMeLoggedOut()
        {
            var instance = GetInstance(false);
            var result = instance.Controller.Me().Result;
            Assert.True(IsBadRequest(result.Result) || IsNotFound(result.Result)); //This may not always be a bad request!
            Assert.Null(result.Value);
        }

        [Fact]
        public void TestUserSelfDeleteFail()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.Delete(instance.User.id).Result;
            Assert.False(IsSuccessRequest(result));
        }

        [Fact]
        public void TestRandomUserDeleteFail()
        {
            var instance = GetInstance(false);
            var user = instance.Context.Users.Last(x => x.role == Role.None); //Just get SOMEONE with no role
            var result = instance.Controller.Delete(user.id).Result;
            Assert.False(IsSuccessRequest(result));
        }

        [Fact]
        public void TestGetUsers()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.Get(new CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            List<UserView> users = ((IEnumerable<UserView>)result.Value["collection"]).ToList();
            Assert.True(users.Count > 0);
            Assert.Contains(users, x => x.id == instance.User.id);
        }

        [Fact]
        public void TestGetUserSingle()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.GetSingle(instance.User.id).Result;
            Assert.True(IsSuccessRequest(result));
            Assert.True(result.Value.id == instance.User.id);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserCreate(bool loggedIn)
        {
            var instance = GetInstance(loggedIn);
            var creds = GetNewCredentials();
            var result = instance.Controller.PostCredentials(creds).Result;
            Assert.True(IsSuccessRequest(result));
            Assert.True(result.Value.id > 0);
        }

        private void DoEmail(ControllerInstance<UsersController> instance, UserCredential creds)
        {
            var result = instance.Controller.SendRegistrationEmail(new UsersController.RegistrationData() {email = creds.email}).Result;
            Assert.True(IsOkRequest(result));
            var code = instance.Emailer.Emails.Last(x => x.Recipients.Contains(creds.email)).Body;
            var final = instance.Controller.ConfirmEmail(new UsersController.ConfirmationData() {confirmationKey = code}).Result;
            Assert.True(IsOkRequest(final));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserEmail(bool loggedIn)
        {
            var instance = GetInstance(loggedIn);
            var creds = GetNewCredentials();
            var user = instance.Controller.PostCredentials(creds).Result;
            DoEmail(instance, creds);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserAuthenticate(bool loggedIn)
        {
            var instance = GetInstance(loggedIn);
            var creds = GetNewCredentials();
            var user = instance.Controller.PostCredentials(creds).Result;
            DoEmail(instance, creds);
            var result = instance.Controller.Authenticate(creds).Result;
            Assert.True(IsSuccessRequest(result));
            Assert.True(!string.IsNullOrWhiteSpace(result.Value));
        }
    }
}