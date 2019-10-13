using System;
using System.IO;
using contentapi.Models;
using contentapi.Services;
using contentapi.test.Overrides;
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

    /*public class TestControllerTest : ControllerTestBase<OpenController>
    {
        public TestControllerTest() { }

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
            Assert.True(instance.Controller.GetCurrentUserAsync().Result.role == Role.None);
            instance = GetInstance(true, Role.SiteAdministrator);
            Assert.True(instance.Controller.GetCurrentUserAsync().Result.role == Role.SiteAdministrator);

            //An extra test just for fun... kinda integration test?
            Assert.True(instance.Controller.CanUserAsync(Permission.CreateCategory).Result);
        }
    }*/
}