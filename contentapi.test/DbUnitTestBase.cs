using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using contentapi.Db;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace contentapi.test;
public class DbUnitTestBase : UnitTestBase, IDisposable
{
    protected IDbConnection masterConnection;
    public readonly string MasterConnectionString;
    public const string DbMigrationFolder = "dbmigrations";
    public const string DbMigrationBlob = "*.sql";
    public static List<string> allQueries = new List<string>();
    public static readonly object allQueryLock = new Object();

    public DbUnitTestBase()
    {
        //Ensure the connection for this particular class is DEFINITELY unique for this class!
        MasterConnectionString = $"Data Source=master_{Guid.NewGuid().ToString().Replace("-", "")};Mode=Memory;Cache=Shared";
        masterConnection = new SqliteConnection(MasterConnectionString);
        masterConnection.Open();

        //Insert database structure here
        var queries = GetAllQueries();

        using(var trans = masterConnection.BeginTransaction())
        {
            foreach(var q in queries)
                masterConnection.QueryMultiple(q, null, trans);
            trans.Commit();
        }

        UpdateServices(s =>
        {
            s.AddTransient<ContentApiDbConnection>(ctx => 
                new ContentApiDbConnection(new SqliteConnection(MasterConnectionString)));
        });
    }

    public IDbConnection CreateNewConnection()
    {
        var result = GetService<ContentApiDbConnection>().Connection; //new SqliteConnection(MasterConnectionString);
        result.Open();
        return result;
    }

    public List<string> GetAllQueries()
    {
        lock(allQueryLock)
        {
            if(allQueries.Count == 0)
            {
                var files = System.IO.Directory.GetFiles(DbMigrationFolder, DbMigrationBlob);
                allQueries = files.Select(x => System.IO.File.ReadAllText(x)).ToList();
            }

            return allQueries;
        }
    }

    public void Dispose()
    {
        masterConnection.Close();
    }
}