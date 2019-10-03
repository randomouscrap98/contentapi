using System;
using Microsoft.Extensions.DependencyInjection;
using contentapi.Configs;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.test.Overrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using contentapi.Services;

namespace contentapi.test
{
    public class TestControllerContext
    {
        public UserCredential SessionCredentials {get;}
        public UserView SessionResult {get;}
        public string SessionAuthToken {get;}

        //We use singleton fakes so we can get data as needed
        public FakeEmailer emailer = null; // = new FakeEmailer();
        public FakeLanguage language = null; //new FakeLanguage();
        public TestSessionService session = null;

        public UsersController userController;


        public TestControllerContext()
        {
            SessionCredentials = GetNewCredentials();

            var provider = GetProvider();
            userController = GetUsersController();

            //Always create the user. Setting the sessionresult makes ALL controllers created by
            //the provider use this user.
            SessionResult = CreateUser(SessionCredentials);
            SendAuthEmail(SessionCredentials);
            ConfirmUser(SessionCredentials);
            SessionAuthToken = AuthenticateUser(SessionCredentials);
        }

        public UserView CreateUser(UserCredential user)
        {
            var thing = userController.Post(user).Result;
            return thing.Value;
        }

        public ActionResult SendAuthEmail(UserCredential user)
        {
            return userController.SendRegistrationEmail(new UsersController.RegistrationData() {email = user.email}).Result;
        }

        public ActionResult ConfirmUser(UserCredential user)
        {
            return userController.ConfirmEmail(new UsersController.ConfirmationData() {
                confirmationKey = emailer.Emails.Last(x => x.Recipients.Contains(user.email)).Body
            }).Result;
        }

        public string AuthenticateUser(UserCredential user)
        {
            return userController.Authenticate(user).Result.Value;
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

        public UsersController GetUsersController()
        {
            return (UsersController)ActivatorUtilities.CreateInstance(GetProvider(), typeof(UsersController));
        }

        public IServiceCollection GetServices()
        {
            var services = new ServiceCollection();
            var startup = new Startup(null);
            startup.ConfigureBasicServices(services, new StartupServiceConfig()
            {
                SecretKey = "barelyASecretKey",
                ContentConString = "Data Source=content.db"
            });

            //Unfortunately we can ONLY build the session when we already have our services... kinda funky but whatever,
            //that's how singletons work (and we want to be able to control login/etc.)
            var tempProvider = services.BuildServiceProvider();

            //Use OUR session service instead of whatever the startup provides. This means
            //that whatever user we create to attach to our context becomes the user for ALL
            //controllers (or they should, anyway)
            session = session ?? (TestSessionService)ActivatorUtilities.CreateInstance(tempProvider, typeof(TestSessionService));
            emailer = emailer ?? (FakeEmailer)ActivatorUtilities.CreateInstance(tempProvider, typeof(FakeEmailer));
            language = language ?? (FakeLanguage)ActivatorUtilities.CreateInstance(tempProvider, typeof(FakeLanguage));

            //If for whatever reason these can't be singletons anymore... well, idk
            services.Replace(ServiceDescriptor.Singleton<ISessionService>(session));
            services.Replace(ServiceDescriptor.Singleton<IEmailService>(emailer));
            services.Replace(ServiceDescriptor.Singleton<ILanguageService>(language));

            return services;
        }

        public void SetLoginState(bool loggedIn)
        {
            if(loggedIn)
                session.UidProvider = () => SessionResult?.id;
            else
                session.UidProvider = null;
        }
        public void Login() { SetLoginState(true); }
        public void Logout() { SetLoginState(false); }

        public IServiceProvider GetProvider()
        {
            return GetServices().BuildServiceProvider();
        }

        public bool IsBadRequest(ActionResult result)
        {
            return result is BadRequestResult || result is BadRequestObjectResult;
        }

        public bool IsNotFound(ActionResult result)
        {
            return result is NotFoundObjectResult || result is NotFoundResult;
        }

        public bool IsNotAuthorized(ActionResult result)
        {
            return result is UnauthorizedObjectResult || result is UnauthorizedResult;
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

        //public void TestWhileLoggedIn(Action thing)
        //{
        //    context.Login();
        //    context.Logout();
        //}
    }
}