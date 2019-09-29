using System;
using Microsoft.Extensions.DependencyInjection;

namespace contentapi.test
{
    public class ControllerTestBase
    {
        public IServiceProvider GetServices()
        {
            var services = new ServiceCollection();
            //services.AddDbContext<ContentDbContext>(options => options.UseLazyLoadingProxies().UseSqlite(contentConstring));

            return services.BuildServiceProvider();
        }
    }
}