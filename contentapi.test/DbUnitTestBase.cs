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
    public const string MasterConnectionString = "Data Source=master;Mode=Memory;Cache=Shared";
    public const string DbMigrationFolder = "dbmigrations";
    public const string DbMigrationBlob = "*.sql";
    public static List<string> allQueries = new List<string>();
    public static readonly object allQueryLock = new Object();

    public DbUnitTestBase()
    {
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