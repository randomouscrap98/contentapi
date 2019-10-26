
using System;
using System.Linq;
using contentapi.Configs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace contentapi.test
{
    public class TestBase
    {
        public IServiceCollection GetBaseServices()
        {
            var services = new ServiceCollection();
            var startup = new Startup(null);
            startup.ConfigureBasicServices(services, new StartupServiceConfig()
            {
                SecretKey = "barelyASecretKey",
                ContentConString = "Data Source=content.db"
            });

            services.AddSingleton(LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    //.AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
                    //.AddEventLog();
            }));

            return services;
        }

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
    }
}