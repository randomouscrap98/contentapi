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

namespace contentapi.test
{
    public class UnitTestBase : IDisposable
    {
        public List<SqliteConnection> connections = new List<SqliteConnection>();
        public string SqliteConnectionString = "Data Source=:memory:;";

        protected contentapi.Services.Implementations.DefaultServiceProvider contentApiProvider;

        public UnitTestBase()
        {
            //serviceProvider = new DefaultServiceProvider();
            contentApiProvider = new contentapi.Services.Implementations.DefaultServiceProvider();
        }

        public void Dispose()
        {
            foreach(var con in connections)
            {
                try { con.Close(); }
                catch(Exception) { }
            }
        }

        public virtual IServiceCollection CreateServices()
        {
            //Whhyyyy am I doing it like this.
            var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();
            connections.Append(connection);

            var services = new ServiceCollection();
            var dsp = new Randomous.EntitySystem.Implementations.DefaultServiceProvider();
            services.AddLogging(configure => configure.AddDebug());//configure.AddSerilog(new LoggerConfiguration().WriteTo.File($"{GetType()}.txt").CreateLogger()));
            dsp.AddDefaultServices(
                services, 
                options => options.UseSqlite(connection).EnableSensitiveDataLogging(true),
                d => d.Database.EnsureCreated());

            //EMPTY CONFIGS! Hopefully we won't need to test anything requiring these configs...
            IConfiguration config = new ConfigurationBuilder()
                //.AddJsonFile("appsettings.Development.json", true, true)
                .Build();

            contentApiProvider.AddDefaultServices(services);
            contentApiProvider.AddServiceConfigurations(services, config);
            services.AddSingleton<IEntityProvider, EntityProvider>();   //We want everyone to share data, even in tests. That's because all my tests are bad

            return services;
        }

        protected IServiceProvider serviceProvider = null;
        protected readonly object providerLock = new object();

        public T CreateService<T>()
        {
            lock(providerLock)
            {
                if (serviceProvider == null)
                {
                    var services = CreateServices();
                    serviceProvider = services.BuildServiceProvider();
                }
            }

            return (T)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, typeof(T));
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
            Assert.True(task.Wait(2000));    //We should've gotten signaled. Give the test plenty of time to get the memo
            return task.Result;    //This won't wait at all if the previous came through
        }

        public void AssertNotWait(Task task)
        {
            Assert.False(task.Wait(1));
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

    }
}