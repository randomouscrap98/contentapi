using System;
using Microsoft.Extensions.DependencyInjection;
using contentapi.Configs;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using contentapi.Models;
using contentapi.test.Overrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using contentapi.Services;

namespace contentapi.test
{
    public class ControllerInstance<T> where T : ControllerBase
    {
        public T Controller = null;
        public UserCredential Credentials = null;
        public User User = null;
        public FakeEmailer Emailer = null;
    }

    public class ControllerTestBase<T> where T : ControllerBase
    {
        public ControllerTestBase() { }

        public string UniqueSection()
        {
            return Guid.NewGuid().ToString().Split("-".ToCharArray()).Last();
        }

        public bool IsBadRequest(ActionResult result) { return result is BadRequestResult || result is BadRequestObjectResult; }
        public bool IsNotFound(ActionResult result) { return result is NotFoundObjectResult || result is NotFoundResult; }
        public bool IsNotAuthorized(ActionResult result) { return result is UnauthorizedObjectResult || result is UnauthorizedResult; }
        public bool IsOkRequest(ActionResult result) { return result is OkObjectResult || result is OkResult; }
        public bool IsSuccessRequest<V>(ActionResult<V> result)
        {
            //A VERY BAD CHECK but... eeeggghh
            if(result.Result == null)
                return result.Value != null;
            else
                return IsOkRequest(result.Result);
        }

        public IServiceCollection GetBaseServices()
        {
            var services = new ServiceCollection();
            var startup = new Startup(null);
            startup.ConfigureBasicServices(services, new StartupServiceConfig()
            {
                SecretKey = "barelyASecretKey",
                ContentConString = "Data Source=content.db"
            });

            return services;
        }

        public ControllerInstance<T> GetInstance(bool loggedIn, Role role = Role.None)
        {
            var instance = new ControllerInstance<T>();

            var services = GetBaseServices();
            var tempProvider = services.BuildServiceProvider(); 

            var session = (TestSessionService)ActivatorUtilities.CreateInstance(tempProvider, typeof(TestSessionService));
            var emailer = (FakeEmailer)ActivatorUtilities.CreateInstance(tempProvider, typeof(FakeEmailer));
            var language = (FakeLanguage)ActivatorUtilities.CreateInstance(tempProvider, typeof(FakeLanguage));
            services.Replace(ServiceDescriptor.Singleton<ISessionService>(session));
            services.Replace(ServiceDescriptor.Singleton<IEmailService>(emailer));
            services.Replace(ServiceDescriptor.Singleton<ILanguageService>(language));
            services.AddTransient<T>();

            var realProvider = services.BuildServiceProvider();
            instance.Controller = (T)ActivatorUtilities.CreateInstance(realProvider, typeof(T));
            instance.Emailer = emailer;

            if(loggedIn)
            {
                var creds = GetNewCredentials();
                var user = new User()
                {
                    username = creds.username,
                    email = creds.email,
                    role = role
                };

                var context = (ContentDbContext)realProvider.GetService(typeof(ContentDbContext));
                context.Users.Add(user);
                context.SaveChanges();

                instance.User = user;
                session.UidProvider = () => instance.User.id;
            }

            return instance;
        }

        //public ActionResult ConfirmUser(UserCredential user)
        //{
        //    return userController.ConfirmEmail(new UsersController.ConfirmationData() {
        //        confirmationKey = emailer.Emails.Last(x => x.Recipients.Contains(user.email)).Body
        //    }).Result;
        //}


        public UserCredential GetNewCredentials()
        {
            return new UserCredential()
            {
                username = "user_" + UniqueSection(),
                email = "email_" + UniqueSection() + "@pleseNotBEREAL_FOADDf.com",
                password = "aVeryUniquePassword"
            };
        }
    }
}