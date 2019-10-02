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
    }
}