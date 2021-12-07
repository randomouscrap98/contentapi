using System;
using contentapi.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class UnitTestBase
{
    protected ILogger logger;
    protected IServiceCollection baseCollection;
    protected IServiceProvider baseProvider;
    public const string SecretKey = "Not very secret, now is it? 7483927932";

    public UnitTestBase()//Action<IServiceCollection>? modify = null)
    {
        baseCollection = new ServiceCollection();
        DefaultSetup.AddDefaultServices(baseCollection);
        DefaultSetup.AddSecurity(baseCollection, SecretKey);

        baseCollection.AddLogging(builder => {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

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
        return ActivatorUtilities.GetServiceOrCreateInstance<T>(baseProvider);
    }

    protected void AssertDateClose(DateTime dt1, DateTime? dt2 = null, double seconds = 5)
    {
        var dt2r = dt2 ?? DateTime.UtcNow;
        Assert.True(Math.Abs((dt1 - dt2r).TotalSeconds) < seconds, $"Dates were not within an acceptable closeness in range! DT1: {dt1}, DT2: {dt2r}");
    }

}