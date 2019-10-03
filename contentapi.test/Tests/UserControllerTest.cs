using contentapi.Controllers;
using Xunit;
using contentapi.Models;
using System;

namespace contentapi.test
{
    public class UserControllerTest : ControllerTestBase<UsersController>
    {
        public UserControllerTest(TestControllerContext context) : base(context) {}

        [Fact]
        public void TestBasicUserCreate()
        {
            var credential = context.GetNewCredentials();
            var userResult = controller.Post(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            Assert.True(userResult.Value.id > 0);
            Assert.True(userResult.Value.createDate <= DateTime.Now);
            Assert.True(userResult.Value.createDate > DateTime.Now.AddDays(-1));
        }

        private void TestUserCreateDupe(Action<UserCredential> alterCredential)
        {
            var credential = context.GetNewCredentials();
            var userResult = controller.Post(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            alterCredential(credential);
            userResult = controller.Post(credential).Result;
            Assert.True(context.IsBadRequest(userResult.Result));
        }

        [Fact]
        public void TestUserCreateDupeUsername()
        {
            TestUserCreateDupe((c) => c.email += "a");
        }

        [Fact]
        public void TestUserCreateDupeEmail()
        {
            TestUserCreateDupe((c) => c.username += "a");
        }

        [Fact]
        public void TestUserAuthentivate()
        {
            var result = controller.Authenticate(context.SessionCredentials).Result;
            //Can't check if they're EQUAL, because the expiration will be different.
            //Just make sure we got SOMETHING.
            //Assert.True(context.IsOkRequest(result.Result));
            Assert.False(string.IsNullOrWhiteSpace(result.Value));
        }

        [Fact]
        public void TestUserMe()
        {
            context.Login();
            var result = controller.Me().Result;
            Assert.Equal(context.SessionResult.id, result.Value.id);
            Assert.Equal(context.SessionResult.username, result.Value.username);
        }
    }
}