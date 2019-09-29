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

namespace contentapi.test
{
    public class ControllerContext
    {
        public UserCredential SessionCredentials {get;}
        public string SessionAuthToken {get;}

        public ControllerContext()
        {
            SessionCredentials = GetNewCredentials();

            var controller = GetUsersController();

            //Always create the user
            CreateUser(SessionCredentials, controller);
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

        public UsersTestController GetUsersController()
        {
            var services = GetServices();
            services.AddTransient<UsersTestController>();
            var provider = services.BuildServiceProvider();
            return (UsersTestController)provider.GetService(typeof(UsersTestController));
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

    public class ControllerTestBase<T> : IClassFixture<ControllerContext> where T : ControllerBase
    {
        protected ControllerContext context;
        protected T controller;
        
        public ControllerTestBase(ControllerContext context)
        {
            this.context = context;
            var services = context.GetServices();
            services.AddTransient<T>();
            controller = (T)services.BuildServiceProvider().GetService(typeof(T));
        }
    }
}