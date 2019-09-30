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
using contentapi.test.Overrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using contentapi.Services;

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

            var provider = GetProvider();
            var controller = (UsersTestController)ActivatorUtilities.CreateInstance(provider, typeof(UsersTestController));

            //Always create the user. Setting the sessionresult makes ALL controllers created by
            //the provider use this user.
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
                confirmationKey = controller.ConfirmationEmails.Last(x => x.Item1 == user.email).Item2
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

        public UsersTestController GetUsersController()
        {
            return (UsersTestController)ActivatorUtilities.CreateInstance(GetProvider(), typeof(UsersTestController));
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
            //Use OUR session service instead of whatever the startup provides. This means
            //that whatever user we create to attach to our context becomes the user for ALL
            //controllers (or they should, anyway)
            services.Replace(ServiceDescriptor.Transient<SessionService, TestSessionService>((s) =>
                new TestSessionService((SessionConfig)s.GetService(typeof(SessionConfig)))
                {
                    UidProvider = () => SessionResult?.id
                }
            ));

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