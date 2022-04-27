using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Db;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace contentapi.test;
public class DbUnitTestBase : UnitTestBase, IDisposable
{
    protected SqliteConnection masterConnection;
    protected SqliteConnection masterBackupConnection;
    public readonly string MasterConnectionString;
    public readonly string MasterBackupConnectionString;
    public const string DbMigrationFolder = "dbmigrations";
    public const string DbMigrationBlob = "*.sql";
    public static List<string> allQueries = new List<string>();
    public static readonly object allQueryLock = new Object();

    public DbUnitTestBase()
    {
        //Ensure the connection for this particular class is DEFINITELY unique for this class!
        MasterConnectionString = $"Data Source=master_{Guid.NewGuid().ToString().Replace("-", "")};Mode=Memory;Cache=Shared";
        MasterBackupConnectionString = MasterConnectionString.Replace("master", "master_backup");
        masterConnection = new SqliteConnection(MasterConnectionString);
        masterConnection.Open(); //We need to keep the master connection open so it doesn't delete the in memory database

        //Insert database structure here
        var queries = GetAllQueries();

        //Each of these could have a transaction, sooo
        foreach(var q in queries)
            masterConnection.Execute(q, null); //, trans);

        //using(var trans = masterConnection.BeginTransaction())
        //{
        //    trans.Commit();
        //}

        masterBackupConnection = new SqliteConnection(MasterBackupConnectionString);
        masterBackupConnection.Open(); //We need to keep the master connection open so it doesn't delete the in memory database
        SetBackupNow();

        //masterConnection.Execute("PRAGMA synchronous = OFF");
        //masterConnection.Execute("PRAGMA journal_mode = MEMORY");

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

    public async Task<int> WriteSingle<T>(T thing) where T : class
    {
        using(var conn = CreateNewConnection())
        {
            return await conn.InsertAsync(thing);
        }
    }

    public void SetBackupNow()
    {
        masterConnection.BackupDatabase(masterBackupConnection);
    }

    public void ResetDatabase()
    {
        //I don't know if this will work when the db isn't empty...
        masterBackupConnection.BackupDatabase(masterConnection);
    }

    public List<string> GetAllQueries()
    {
        lock(allQueryLock)
        {
            if(allQueries.Count == 0)
            {
                var files = System.IO.Directory.GetFiles(DbMigrationFolder, DbMigrationBlob);
                allQueries = files.OrderBy(x => x).Select(x => System.IO.File.ReadAllText(x)).ToList();
            }

            return allQueries;
        }
    }

    public void Dispose()
    {
        masterConnection.Close();
        masterBackupConnection.Close();
    }
}