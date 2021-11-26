using System;
using contentapi.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace contentapi.test;

public class UnitTestBase
{
    protected ILogger logger;
    protected IServiceCollection baseCollection;
    protected IServiceProvider baseProvider;

    public UnitTestBase()//Action<IServiceCollection>? modify = null)
    {
        baseCollection = new ServiceCollection();
        DefaultSetup.AddDefaultServices(baseCollection);

        baseCollection.AddLogging(builder => {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        //Just need ANY type from the assembly... yeah... weird
        baseCollection.AddAutoMapper(typeof(Db.User));

        baseProvider = baseCollection.BuildServiceProvider();
        logger = GetService<ILogger<UnitTestBase>>();
    }


    public void UpdateServices(Action<IServiceCollection> modify)
    {
        modify(baseCollection);
        baseProvider = baseCollection.BuildServiceProvider();
    }

    public T GetService<T>()
    {
        //Building the service collection probably takes time, but oh well
        return ActivatorUtilities.GetServiceOrCreateInstance<T>(baseProvider); //(sp, typeof(T));
        //return ServiceProviderUtilities. baseProvider.GetService<T>() ?? throw new InvalidOperationException($"NO SERVICE {typeof(T)}");
    }
}