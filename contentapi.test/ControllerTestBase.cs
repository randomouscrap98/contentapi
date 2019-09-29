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
        public ControllerContext()
        {
            var controller = GetUsersController();
            SessionCredentials = GetNewCredentials();

            //Create the user for this session
            var result = controller.Post(SessionCredentials).Result;

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

        protected UsersTestController GetUsersController()
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

        public bool IsBadRequest<V>(ActionResult<V> result)
        {
            return result.Result is BadRequestResult || result.Result is BadRequestObjectResult;
        }
    }
}