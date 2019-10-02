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
        public UserCredential TestContextCreateUser()
        {
            var result = context.GetNewCredentials();
            var view = context.CreateUser(result);
            Assert.True(view.username == result.username);
            return result;
        }

        [Fact]
        public UserCredential TestContextSendEmail()
        {
            var origin = TestContextCreateUser();
            var result = context.SendAuthEmail(origin);
            Assert.True(context.IsOkRequest(result));
            return origin;
        }

        [Fact]
        public UserCredential TestContextConfirmEmail()
        {
            var origin = TestContextSendEmail();
            var result = context.ConfirmUser(origin);
            Assert.True(context.IsOkRequest(result));
            return origin;
        }

        [Fact]
        public void TestContextAuthenticate()
        {
            var origin = TestContextConfirmEmail();
            var result = context.AuthenticateUser(origin);
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