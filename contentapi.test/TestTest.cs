using System;
using System.IO;
using contentapi.Models;
using contentapi.test.Controllers;
using Xunit;

namespace contentapi.test
{
    public class TestTest 
    {
        [Fact]
        public void DatabaseExists()
        {
            Assert.True(File.Exists("content.db"));
        }
    }

    public class TestControllerTest : IClassFixture<ControllerContext>
    {
        protected ControllerContext context;

        public TestControllerTest(ControllerContext context)
        {
            this.context = context;
        }

        [Fact]
        public Tuple<UserCredential, UsersTestController> TestContextCreateUser()
        {
            var result = Tuple.Create(context.GetNewCredentials(), context.GetUsersController());
            var view = context.CreateUser(result.Item1, result.Item2);
            Assert.True(view.username == result.Item1.username);
            return result;
        }

        [Fact]
        public Tuple<UserCredential, UsersTestController> TestContextSendEmail()
        {
            var origin = TestContextCreateUser();
            var result = context.SendAuthEmail(origin.Item1, origin.Item2);
            Assert.True(context.IsOkRequest(result));
            return origin;
        }

        [Fact]
        public Tuple<UserCredential, UsersTestController> TestContextConfirmEmail()
        {
            var origin = TestContextSendEmail();
            var result = context.ConfirmUser(origin.Item1, origin.Item2);
            Assert.True(context.IsOkRequest(result));
            return origin;
        }

        [Fact]
        public void TestContextAuthenticate()
        {
            var origin = TestContextConfirmEmail();
            var result = context.AuthenticateUser(origin.Item1, origin.Item2);
            Assert.True(!String.IsNullOrWhiteSpace(result));
        }
    }
}