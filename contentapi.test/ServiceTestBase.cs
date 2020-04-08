using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace contentapi.test
{
    public class ServiceTestBase<T> : UnitTestBase
    {
        protected T service;

        public ServiceTestBase()
        {
            service = CreateService<T>();
        }
    }

    public abstract class ServiceConfigTestBase<T,C> : ServiceTestBase<T> where C : class //, new()
    {
        protected abstract C config {get;} //= new C();

        public override IServiceCollection CreateServices()
        {
            var services = base.CreateServices();
            services.AddSingleton(config);
            return services;
        }
    }
}