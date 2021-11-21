using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    public class OptionProvider<C> : IOptionsMonitor<C>
    {
        public C UnderlyingValue;
        public C CurrentValue => UnderlyingValue;
        public C Get(string name) { return UnderlyingValue; }

        public IDisposable OnChange(Action<C, string> listener)
        {
            return null;
        }
    }

    public abstract class ServiceConfigTestBase<T,C> : ServiceTestBase<T> where C : class, new()
    {
        protected abstract C config {get;} //= new C();

        public override IServiceCollection CreateServices()
        {
            var services = base.CreateServices();
            services.AddSingleton(config);
            services.AddSingleton<IOptionsMonitor<C>>(new OptionProvider<C>() {UnderlyingValue = config});
            //services.Configure(new Action<C>((c) => config));
            //services.AddSingleton(Options.Create(config));
            return services;
        }
    }
}