using contentapi.Controllers;
using Xunit;
using contentapi.Models;
using System;
using contentapi.Services;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace contentapi.test
{
    public class UserControllerTest : ControllerTestBase<UsersController>
    {
        public UserView CreateUser()
        {
            var instance = GetInstance(false);
            var credential = GetNewCredentials();
            var userResult = instance.Controller.PostCredentials(credential).Result;
            return userResult.Value;
        }

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
            Assert.Equal(instance.User.entityId, result.Value.id);
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestGetUsers(bool loggedIn)
        {
            //To have at least ONE user
            CreateUser();

            var instance = GetInstance(loggedIn);
            var result = instance.Controller.Get(new CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            List<UserView> users = instance.Controller.GetCollectionFromResult<UserView>(result.Value).ToList();
            Assert.True(users.Count > 0);

            if(loggedIn)
                Assert.Contains(users, x => x.id == instance.User.entityId);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestGetManyUsers(bool loggedIn)
        {
            const int userCount = 5;

            for(int i = 0; i < userCount; i++)
                CreateUser();

            var instance = GetInstance(loggedIn);
            var result = instance.Controller.Get(new CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            List<UserView> users = instance.Controller.GetCollectionFromResult<UserView>(result.Value).ToList();
            Assert.True(users.Count >= userCount);

            if(loggedIn)
                Assert.Contains(users, x => x.id == instance.User.entityId);
        }

        [Fact]
        public void TestGetUserSingle()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.GetSingle(instance.User.entityId).Result;
            Assert.True(IsSuccessRequest(result));
            Assert.True(result.Value.id == instance.User.entityId);
        }

        [Fact]
        public void TestUserSelfDeleteFail()
        {
            CreateUser();

            var instance = GetInstance(true);
            var result = instance.Controller.Delete(instance.User.entityId).Result;
            Assert.False(IsSuccessRequest(result));
        }

        [Fact]
        public void TestRandomUserDeleteFail()
        {
            var user = CreateUser();

            var instance = GetInstance(false);
            //var user = instance.Context.UserEntities.Last(x => x.role == Role.None); //Just get SOMEONE with no role
            var result = instance.Controller.Delete(user.id).Result;
            Assert.False(IsSuccessRequest(result));
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