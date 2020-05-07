using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
        protected DefaultServiceProvider serviceProvider;
        protected contentapi.Services.Keys keys;

        public UnitTestBase()
        {
            serviceProvider = new DefaultServiceProvider();
            contentApiProvider = new contentapi.Services.Implementations.DefaultServiceProvider();
            this.keys = CreateService<contentapi.Services.Keys>();
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
            services.AddLogging(configure => configure.AddDebug());//configure.AddSerilog(new LoggerConfiguration().WriteTo.File($"{GetType()}.txt").CreateLogger()));
            serviceProvider.AddDefaultServices(
                services, 
                options => options.UseSqlite(connection).EnableSensitiveDataLogging(true),
                d => d.Database.EnsureCreated());

            contentApiProvider.AddDefaultServices(services);

            //you'll NEED to add the default configurations at some point too!!!

            return services;
        }

        public T CreateService<T>()
        {
            var services = CreateServices();
            var provider = services.BuildServiceProvider();
            return (T)ActivatorUtilities.GetServiceOrCreateInstance(provider, typeof(T));
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