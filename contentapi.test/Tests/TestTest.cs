using System;
using System.IO;
using contentapi.Models;
using contentapi.Services;
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

    public class TestControllerTest : ControllerTestBase<OpenController> //IClassFixture<TestControllerContext>
    {
        //protected TestControllerContext context;

        public TestControllerTest() //TestControllerContext context)
        {
            //this.context = context;
        }

        [Fact]
        public void TestGetInstanceSimple()
        {
            var instance = GetInstance(false);
            Assert.True(instance.Controller.GetType() == typeof(OpenController));
        }

        [Fact]
        public void TestGetInstanceLogin()
        {
            var instance = GetInstance(true);
            Assert.True(instance.Controller.GetUid() == instance.User.id);
        }

        [Fact]
        public void TestGetInstanceRole()
        {
            var instance = GetInstance(true, Role.None);
            Assert.True(instance.Controller.GetCurrentUserAsync().Result.role == Role.None); //instance.Controller.CanUserAsync(Permission.CreateCategory).Result);
            instance = GetInstance(true, Role.SiteAdministrator);
            Assert.True(instance.Controller.GetCurrentUserAsync().Result.role == Role.SiteAdministrator); //instance.Controller.CanUserAsync(Permission.CreateCategory).Result);

            //An extra test just for fun... kinda integration test?
            Assert.True(instance.Controller.CanUserAsync(Permission.CreateCategory).Result);
        }

        //[Fact]
        //public UserCredential TestContextCreateUser()
        //{
        //    var result = context.GetNewCredentials();
        //    var view = context.CreateUser(result);
        //    Assert.True(view.username == result.username);
        //    return result;
        //}

        //[Fact]
        //public UserCredential TestContextSendEmail()
        //{
        //    var origin = TestContextCreateUser();
        //    var result = context.SendAuthEmail(origin);
        //    Assert.True(context.IsOkRequest(result));
        //    return origin;
        //}

        //[Fact]
        //public UserCredential TestContextConfirmEmail()
        //{
        //    var origin = TestContextSendEmail();
        //    var result = context.ConfirmUser(origin);
        //    Assert.True(context.IsOkRequest(result));
        //    return origin;
        //}

        //[Fact]
        //public void TestContextAuthenticate()
        //{
        //    var origin = TestContextConfirmEmail();
        //    var result = context.AuthenticateUser(origin);
        //    Assert.True(!String.IsNullOrWhiteSpace(result));
        //}

        //[Fact]
        //public void TestContextSession()
        //{
        //    Assert.True(context.SessionResult.id > 0);
        //    Assert.False(String.IsNullOrWhiteSpace(context.SessionResult.username));
        //    Assert.False(String.IsNullOrWhiteSpace(context.SessionAuthToken));
        //}

        //[Fact]
        //public void TestControllerUidLinking()
        //{
        //    context.Login(); //make sure we're logged in!
        //    var controller = (OpenController)ActivatorUtilities.CreateInstance(context.GetProvider(), typeof(OpenController));
        //    Assert.True(controller.GetUid() == context.SessionResult.id);
        //    context.Logout();
        //    Assert.True(controller.GetUid() == -1);
        //}

        //[Fact]
        //public void TestSetRole()
        //{
        //    var user = context.CreateNewUser();
        //    Assert.Equal(Role.None, context.context.Users.Find(user.id).role);
        //    context.SetRoleAsync(Role.SiteAdministrator, user.id).Wait();
        //    Assert.Equal(Role.SiteAdministrator, context.context.Users.Find(user.id).role);
        //}

        //[Fact]
        //public void TestRunAs()
        //{
        //    context.Login();
        //    var controller = (OpenController)ActivatorUtilities.CreateInstance(context.GetProvider(), typeof(OpenController));
        //    Assert.Equal(Role.None, controller.GetCurrentUserAsync().Result.role);
        //    context.RunAs(Role.SiteAdministrator, () =>
        //    {
        //        Assert.Equal(Role.SiteAdministrator, controller.GetCurrentUserAsync().Result.role);
        //    });
        //    Assert.Equal(Role.None, controller.GetCurrentUserAsync().Result.role);
        //}
    }
}