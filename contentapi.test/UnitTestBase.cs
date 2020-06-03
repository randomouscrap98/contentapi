using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Implementations;
using Xunit;

//[assembly: CollectionBehavior(MaxParallelThreads = 1)] //this broke EVERYTHING, it MIGHT set the threadpool for .NET ENTIRELY to 1!!!
//[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace contentapi.test
{
    [CollectionDefinition("ASYNC", DisableParallelization = true)]
    public class UnitTestBase : IDisposable
    {
        protected SqliteConnection connection;
        protected List<IServiceScope> scopes;

        public UnitTestBase()
        {
            //Hmmm, use only one connection per test. This is because each new connection to an in-memory
            //database... is a new database lol.
            connection = new SqliteConnection("Data Source=:memory:;");
            connection.Open();
            scopes = new List<IServiceScope>();
        }

        public void Dispose()
        {
            connection.Close();

            foreach(var s in scopes)
            {
                try { s.Dispose(); }
                catch { /* do nothing */ }
            }
        }

        public virtual IServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            var csp = new contentapi.Services.Implementations.DefaultServiceProvider();
            var dsp = new Randomous.EntitySystem.Implementations.DefaultServiceProvider();
            services.AddLogging(configure => configure.AddDebug());
            dsp.AddDefaultServices(
                services, 
                options => options.UseSqlite(connection).EnableSensitiveDataLogging(true),
                d => d.Database.EnsureCreated());

            //EMPTY CONFIGS! Hopefully we won't need to test anything requiring these configs...
            IConfiguration config = new ConfigurationBuilder()
                //.AddJsonFile("appsettings.Development.json", true, true)
                .Build();

            csp.AddDefaultServices(services);
            csp.AddServiceConfigurations(services, config);
            services.AddSingleton<ISignaler<EntityBase>, SignalSystem<EntityBase>>(); //Why must I do this every time?

            return services;
        }

        protected IServiceProvider serviceProvider = null;
        protected IServiceScopeFactory scopeFactory = null;
        protected readonly object providerLock = new object();

        public T CreateService<T>(bool newScope = false)
        {
            lock(providerLock)
            {
                if (serviceProvider == null)
                {
                    var services = CreateServices();
                    serviceProvider = services.BuildServiceProvider();
                    scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                }
            }

            var sp = serviceProvider;

            if(newScope)
            {
                var scope = scopeFactory.CreateScope();
                sp = scope.ServiceProvider;
                scopes.Add(scope);
            }

            return (T)ActivatorUtilities.GetServiceOrCreateInstance(sp, typeof(T));
        }

        public void AssertThrows<T>(Action a) where T : Exception
        {
            try
            {
                a();
                Assert.True(false, "Action was supposed to throw an exception!");
            }
            catch(T)
            {
                //it's ok
            }
        }

        public T AssertWait<T>(Task<T> task)
        {
            Assert.True(task.Wait(500), $"The task returning {typeof(T)} was supposed to complete! Status: {task.Status}, Exception: {task.Exception}");    //We should've gotten signaled. Give the test plenty of time to get the memo
            return task.Result;    //This won't wait at all if the previous came through
        }

        public void AssertWaitThrows<T>(Task task)
        {
            try
            {
                var complete = task.Wait(500);
                Assert.False(true, $"The task didn't throw! Complete: {complete}");
            }
            catch(Exception ex)
            {
                Assert.True(ex is T || ex is AggregateException && ex.InnerException is T, $"Task exception should've been {typeof(T)}, was: {ex}");
            }
        }

        public void AssertNotWait(Task task)
        {
            Assert.False(task.Wait(100));
            Assert.False(task.IsCompleted);
        }

        protected void AssertResultsEqual<T>(IEnumerable<T> expected, IEnumerable<T> result)
        {
            Assert.Equal(expected.Count(), result.Count());
            Assert.Equal(expected.ToHashSet(), result.ToHashSet());
        }

        protected EntityPackage NewPackage()
        {
            return new EntityPackage()
            {
                Entity = NewEntity()
            };
        }

        protected Entity NewEntity(long id = 0, string type = "type")
        {
            return new Entity()
            {
                id = id,
                type = type
            };
        }

        protected void AssertAllowed<T>(Action action, bool allowed)
        {
            try
            {
                action();
                Assert.True(allowed, "Should have thrown an exception!");
            }
            catch(Exception ex)
            {
                Assert.False(allowed);
                Assert.True(ex is T || ex is AggregateException && ex.InnerException is T, $"Exception should've been {typeof(T)}, was: {ex}");
            }
        }
    }
}