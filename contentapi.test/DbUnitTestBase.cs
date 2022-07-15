using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Main;
using contentapi.Search;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.Sqlite;

namespace contentapi.test;
public class DbUnitTestBase : UnitTestBase, IDisposable
{
    protected SqliteConnection masterConnection;
    protected SqliteConnection masterBackupConnection;
    public const string DbMigrationFolder = "dbmigrations";
    public const string DbMigrationBlob = "*.sql";
    public static List<string> allQueries = new List<string>();
    public static readonly object allQueryLock = new Object();

    private ConcurrentBag<GenericSearcher> spawnedSearches = new ConcurrentBag<GenericSearcher>();
    private ConcurrentBag<DbWriter> spawnedWriters = new ConcurrentBag<DbWriter>();
    private ConcurrentBag<IDbConnection> spawnedConnections = new ConcurrentBag<IDbConnection>();

    public IDbServicesFactory dbFactory;

    public DbUnitTestBase() 
    {
        //Ensure the connection for this particular class is DEFINITELY unique for this class!
        masterConnection = new SqliteConnection(MasterConnectionString);
        masterConnection.Open(); //We need to keep the master connection open so it doesn't delete the in memory database

        //Insert database structure here
        var queries = GetAllQueries();

        //Each of these could have a transaction, sooo
        foreach(var q in queries)
            masterConnection.Execute(q, null); //, trans);

        masterBackupConnection = new SqliteConnection(MasterBackupConnectionString);
        masterBackupConnection.Open(); //We need to keep the master connection open so it doesn't delete the in memory database
        SetBackupNow();

        dbFactory = GetService<IDbServicesFactory>();
    }

    public async Task<int> WriteSingle<T>(T thing) where T : class
    {
        using(var conn = dbFactory.CreateRaw())
        {
            conn.Open();
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

    public GenericSearcher GetGenericSearcher()
    {
        var searcher = (GenericSearcher)dbFactory.CreateSearch(); 
        spawnedSearches.Add(searcher);
        return searcher;
    }

    public DbWriter GetWriter()
    {
        var writer = (DbWriter)dbFactory.CreateWriter(); 
        spawnedWriters.Add(writer);
        return writer;
    }

    public IDbConnection GetConnection()
    {
        var con = dbFactory.CreateRaw();
        con.Open();
        spawnedConnections.Add(con);
        return con;
    }

    public void Dispose()
    {
        masterConnection.Close();
        masterBackupConnection.Close();

        while(!spawnedSearches.IsEmpty)
        {
            GenericSearcher item;
            if(spawnedSearches.TryTake(out item!))
                item.Dispose();
        }
        while(!spawnedWriters.IsEmpty)
        {
            DbWriter item;
            if(spawnedWriters.TryTake(out item!))
                item.Dispose();
        }
        while(!spawnedConnections.IsEmpty)
        {
            IDbConnection item;
            if(spawnedConnections.TryTake(out item!))
                item.Dispose();
        }
    }
}