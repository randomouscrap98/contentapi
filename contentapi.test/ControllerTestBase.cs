using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using contentapi.Configs;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.test.Controllers;
using Microsoft.AspNetCore.Http;

namespace contentapi.test
{
    public class TestControllerContext
    {
        public UserCredential SessionCredentials {get;}
        public UserView SessionResult {get;}
        public string SessionAuthToken {get;}

        public TestControllerContext()
        {
            SessionCredentials = GetNewCredentials();

            var controller = GetUsersController();

            //Always create the user
            SessionResult = CreateUser(SessionCredentials, controller);
            SendAuthEmail(SessionCredentials, controller);
            ConfirmUser(SessionCredentials, controller);
            SessionAuthToken = AuthenticateUser(SessionCredentials, controller);
        }

        public UserView CreateUser(UserCredential user, UsersTestController controller)
        {
            return controller.Post(user).Result.Value;
        }

        public ActionResult SendAuthEmail(UserCredential user, UsersTestController controller)
        {
            return controller.SendRegistrationEmail(new UsersController.RegistrationData() {email = user.email}).Result;
        }

        public ActionResult ConfirmUser(UserCredential user, UsersTestController controller)
        {
            return controller.ConfirmEmail(new UsersController.ConfirmationData() {
                confirmationKey = UsersTestController.ConfirmationEmails.Last(x => x.Item1 == user.email).Item2
            }).Result;
        }

        public string AuthenticateUser(UserCredential user, UsersTestController controller)
        {
            return controller.Authenticate(user).Result.Value;
        }

        public string UniqueSection()
        {
            return Guid.NewGuid().ToString().Split("-".ToCharArray()).Last();
        }

        public UserCredential GetNewCredentials()
        {
            return new UserCredential()
            {
                username = "user_" + UniqueSection(),
                email = "email_" + UniqueSection() + "@pleseNotBEREAL_FOADDf.com",
                password = "aVeryUniquePassword"
            };
        }

        public UsersTestController GetUsersController(bool setSession = false)
        {
            var services = GetServices();
            services.AddTransient<UsersTestController>();
            var provider = services.BuildServiceProvider();
            var controller = (UsersTestController)provider.GetService(typeof(UsersTestController));
            if(setSession) controller.DesiredUserId = SessionResult.id;
            return controller;
        }

        public IServiceCollection GetServices()
        {
            var services = new ServiceCollection();
            var startup = new Startup(null);
            startup.ConfigureBasicServices(services, new StartupServiceConfig()
            {
                SecretKey = "barelyASecretKey",
                EmailConfig = new EmailConfig() { },
                ContentConString = "Data Source=content.db"
            });
            services.AddTransient<TestSessionService>();

            return services;
        }

        public IServiceProvider GetProvider()
        {
            return GetServices().BuildServiceProvider();
        }

        public bool IsBadRequest(ActionResult result)
        {
            return result is BadRequestResult || result is BadRequestObjectResult;
        }

        public bool IsOkRequest(ActionResult result)
        {
            return result is OkObjectResult || result is OkResult;
        }
    }

    public class ControllerTestBase<T> : IClassFixture<TestControllerContext> where T : ControllerBase
    {
        protected TestControllerContext context;
        protected T controller;
        
        public ControllerTestBase(TestControllerContext context)
        {
            this.context = context;
            var services = context.GetServices();
            services.AddTransient<T>();
            controller = (T)services.BuildServiceProvider().GetService(typeof(T));
        }

        //public ControllerContext GetContext()
        //{
        //    return new ControllerContext()
        //    {
        //        HttpContext = new DefaultHttpContext()
        //        {
        //            User = context.Sess
        //        }
        //    };
        //}
    }
}