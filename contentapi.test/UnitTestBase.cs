using System;
using System.Data;
using System.Linq;
using System.Threading;
using contentapi.Db;
using contentapi.History;
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

    public void AssertSnapshotsEqual(ContentSnapshot expected, ContentSnapshot actual)
    {
        Assert.Equal(expected.id, actual.id);
        Assert.Equal(expected.name, actual.name);
        Assert.Equal(expected.createDate, actual.createDate);
        Assert.Equal(expected.description, actual.description);
        Assert.Equal(expected.text, actual.text);
        Assert.Equal(expected.createUserId, actual.createUserId);
        Assert.Equal(expected.values.Count, actual.values.Count);
        Assert.Equal(expected.keywords.Count, actual.keywords.Count);
        Assert.Equal(expected.permissions.Count, actual.permissions.Count);
        expected.values.ForEach(x => {
            var v = actual.values.First(y => y.id == x.id);
            AssertContentValuesEqual(x, v);
        });
        expected.keywords.ForEach(x => {
            var v = actual.keywords.First(y => y.id == x.id);
            AssertContentKeywordsEqual(x, v);
        });
        expected.permissions.ForEach(x => {
            var v = actual.permissions.First(y => y.id == x.id);
            AssertContentPermissionsEqual(x, v);
        });
    }

    public void AssertContentValuesEqual(ContentValue expected, ContentValue actual)
    {
        Assert.Equal(expected.id, actual.id);
        Assert.Equal(expected.key, actual.key);
        Assert.Equal(expected.value, actual.value);
    }

    public void AssertContentKeywordsEqual(ContentKeyword expected, ContentKeyword actual)
    {
        Assert.Equal(expected.id, actual.id);
        Assert.Equal(expected.value, actual.value);
    }

    public void AssertContentPermissionsEqual(ContentPermission expected, ContentPermission actual)
    {
        Assert.Equal(expected.id, actual.id);
        Assert.Equal(expected.userId, actual.userId);
        Assert.Equal(expected.create, actual.create);
        Assert.Equal(expected.update, actual.update);
        Assert.Equal(expected.delete, actual.delete);
        Assert.Equal(expected.read, actual.read);
    }

    public string GetTempFolder(bool create = true)
    {
        var folder = System.IO.Path.Combine("UnitTestTempFiles", Guid.NewGuid().ToString());
        if(create) System.IO.Directory.CreateDirectory(folder);
        return folder;
    }

}