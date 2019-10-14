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
        public ContentDbContext Context = null;
        public IEntityService EntityService = null;
    }

    public class ControllerTestBase<T> : TestBase where T : ControllerBase
    {
        public ControllerTestBase() { }


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
            instance.Context = (ContentDbContext)realProvider.GetService(typeof(ContentDbContext));
            instance.EntityService = (IEntityService)realProvider.GetService(typeof(IEntityService));

            if(loggedIn)
            {
                var creds = GetNewCredentials();
                var user = new User()
                {
                    username = creds.username,
                    email = creds.email,
                    role = role
                };

                instance.EntityService.SetNewEntity(user);

                instance.Context.Users.Add(user);
                instance.Context.SaveChanges();

                instance.User = user;
                session.UidProvider = () => instance.User.entityId;
            }

            return instance;
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
    }
}