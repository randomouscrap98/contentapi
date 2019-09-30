using System;
using System.IO;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.test.Overrides;
using Microsoft.Extensions.DependencyInjection;
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

    public class TestControllerTest : IClassFixture<TestControllerContext>
    {
        protected TestControllerContext context;

        public TestControllerTest(TestControllerContext context)
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

        [Fact]
        public void TestContextSession()
        {
            Assert.True(context.SessionResult.id > 0);
            Assert.False(String.IsNullOrWhiteSpace(context.SessionResult.username));
            Assert.False(String.IsNullOrWhiteSpace(context.SessionAuthToken));
        }

        [Fact]
        public void TestControllerUidLinking()
        {
            var controller = (OpenController)ActivatorUtilities.CreateInstance(context.GetProvider(), typeof(OpenController));
            Assert.True(controller.GetUid() == context.SessionResult.id);
        }
    }
}