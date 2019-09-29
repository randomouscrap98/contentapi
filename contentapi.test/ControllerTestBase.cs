using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using contentapi.Configs;
using Microsoft.Extensions.Configuration;

namespace contentapi.test
{
    public class ControllerTestBase<T> where T : class
    {
        protected T controller;

        public ControllerTestBase()
        {
            var services = GetServices();
            services.AddTransient<T>();
            controller = (T)services.BuildServiceProvider().GetService(typeof(T));
        }

        public IServiceCollection GetServices()
        {
            //var config = new ConfigurationBuilder();
            //config.AddJsonFile("appsettings.Development.json");
            var services = new ServiceCollection();
            var startup = new Startup(null);
            startup.ConfigureBasicServices(services, new StartupServiceConfig()
            {
                SecretKey = "barelyASecretKey",
                EmailConfig = new EmailConfig() { },
                ContentConString = "Data Source=content.db"
            });
            //services.AddDbContext<ContentDbContext>(options => options.UseLazyLoadingProxies().UseSqlite("Data Source=content.db"));

            return services;
        }

        public IServiceProvider GetProvider()
        {
            return GetServices().BuildServiceProvider();
        }
    }
}