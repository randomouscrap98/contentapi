using System;
using Microsoft.Extensions.DependencyInjection;
using contentapi.Configs;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using contentapi.Models;
using contentapi.test.Overrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using contentapi.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace contentapi.test
{
    public class ControllerInstance<T> where T : ControllerBase
    {
        public T Controller = null;
        public UserCredential Credentials = null;
        public UserEntity User = null;
        public FakeEmailer Emailer = null;
        public ContentDbContext Context = null;
        public IEntityService EntityService = null;
        public QueryService QueryService = null;
        public ILogger Logger = null;
    }

    public class ControllerTestBase<T> : TestBase where T : ControllerBase
    {
        public ControllerTestBase() { }
        public ControllerTestBase(TestBase copy) : base (copy) { }


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
            instance.QueryService = (QueryService)realProvider.GetService(typeof(QueryService));
            var factory = (ILoggerFactory)realProvider.GetService(typeof(ILoggerFactory));
            instance.Logger = factory.CreateLogger(GetType());

            if(loggedIn)
            {
                var creds = GetNewCredentials();
                var user = new UserEntity()
                {
                    username = creds.username,
                    email = creds.email,
                    role = role
                };

                instance.EntityService.SetNewEntity(user);

                user.Entity.baseAllow = EntityAction.Read;

                instance.Context.UserEntities.Add(user);
                instance.Context.SaveChanges();

                //// Get call stack
                //StackTrace stackTrace = new StackTrace();
                //var caller = stackTrace.GetFrame(1).GetMethod().Name;
                //instance.Logger.LogWarning($"({caller}) Users: {string.Join(",", instance.Context.UserEntities.Select(x => x.entityId))}({instance.Context.UserEntities.Count()})");

                instance.User = user;
                session.UidProvider = () => instance.User.entityId;
            }

            return instance;
        }

        public ControllerInstance<T> GetBasicInstance()
        {
            return GetInstance(false);
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