using System;
using System.Data;
using System.Threading;
using contentapi.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class UnitTestBase
{
    protected ILogger logger;
    protected IServiceCollection baseCollection;
    protected IServiceProvider baseProvider;

    protected CancellationTokenSource cancelSource;
    protected CancellationTokenSource safetySource;

    public const string SecretKey = "Not very secret, now is it? 7483927932";

    //This sucks, it used to be in DbUnitTestBase
    public readonly string MasterConnectionString;
    public readonly string MasterBackupConnectionString;

    public UnitTestBase()//bool defaultSetup = true) //Func<IDbConnection>? connectionBuilder = null)
    {
        MasterConnectionString = $"Data Source=master_{Guid.NewGuid().ToString().Replace("-", "")};Mode=Memory;Cache=Shared";
        MasterBackupConnectionString = MasterConnectionString.Replace("master", "master_backup");

        baseCollection = new ServiceCollection();
        cancelSource = new CancellationTokenSource();
        safetySource = new CancellationTokenSource();
        safetySource.CancelAfter(5000);

        baseCollection.AddLogging(builder => {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        DefaultSetup.OneTimeSetup();

        DefaultSetup.AddDefaultServices(baseCollection, () => new SqliteConnection(MasterConnectionString));
        DefaultSetup.AddSecurity(baseCollection, SecretKey);

        baseProvider = baseCollection.BuildServiceProvider();
        logger = GetService<ILogger<UnitTestBase>>();
    }

    //public void UpdateServices(Action<IServiceCollection> modify)
    //{
    //    modify(baseCollection);
    //    baseProvider = baseCollection.BuildServiceProvider();
    //}

    public T GetService<T>()
    {
        //Building the service collection probably takes time, but oh well
        return ActivatorUtilities.GetServiceOrCreateInstance<T>(baseProvider);
    }

    protected void AssertDateClose(DateTime dt1, DateTime? dt2 = null, double seconds = 5)
    {
        dt1 = dt1.ToUniversalTime();
        var dt2r = (dt2 ?? DateTime.Now).ToUniversalTime();
        Assert.True(Math.Abs((dt1 - dt2r).TotalSeconds) < seconds, $"Dates were not within an acceptable closeness in range! DT1: {dt1}, DT2: {dt2r}");
    }

    //public GenericSearcher GetGenericSearcher()
    //{
    //    return new GenericSearcher(GetService<ILogger<GenericSearcher>>(), 
    //        GetService<ContentApiDbConnection>(), GetService<IViewTypeInfoService>(), GetService<GenericSearcherConfig>(),
    //        GetService<IMapper>(), GetService<IQueryBuilder>(), 
    //        GetService<IPermissionService>());
    //}

}