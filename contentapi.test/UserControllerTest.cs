using contentapi.Controllers;
using Xunit;
using contentapi.Models;
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using contentapi.test.Controllers;

namespace contentapi.test
{
    public class UserControllerTest : ControllerTestBase<UsersTestController>
    {
        public UserControllerTest(ControllerContext context) : base(context) {}

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