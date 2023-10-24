using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace contentapi.Utilities;

public class SimpleSqliteValueStore : IValueStore
{
    protected ILogger logger;
    protected string constring;
    private readonly IDbConnection masterConnection;

    public const string TABLENAME = "valuestore";
    public const string KEYCOLUMN = "vs_key";
    public const string VALUECOLUMN = "vs_value";

    public SimpleSqliteValueStore(string constring, ILogger<SimpleSqliteValueStore> logger)
    {
        this.logger = logger;
        this.constring = constring;

        //Need a master connection to keep the database alive IF it's in memory. If it's not... oh well.
        masterConnection = new SqliteConnection(this.constring);
        masterConnection.Open();

        using(var trans = masterConnection.BeginTransaction())
        {
            //Go connect right now and create the tables
            masterConnection.Execute(@$"
                CREATE TABLE IF NOT EXISTS ""{TABLENAME}"" (
                    {KEYCOLUMN} TEXT,
                    {VALUECOLUMN} TEXT
                )",
                transaction : trans);
            masterConnection.Execute(@$"CREATE INDEX IF NOT EXISTS idx_key ON {TABLENAME}({KEYCOLUMN})",
                transaction: trans);
            trans.Commit();
        }
    }

    public void Dispose()
    {
        masterConnection.Close();
    }

    public T Get<T>(string key, T defaultValue)
    {
        using(var connection = new SqliteConnection(constring))
        {
            var result = connection.Query<string>($"select {VALUECOLUMN} from {TABLENAME} where {KEYCOLUMN} = @key", new { key }).ToList();
            if(result.Count != 1)
                return defaultValue;
            return JsonConvert.DeserializeObject<T>(result.First())!;
        }
    }

    public void Set<T>(string key, T value)
    {
        using(var connection = new SqliteConnection(constring))
        {
            var svalue = JsonConvert.SerializeObject(value);
            var existing = connection.ExecuteScalar<int>($"select count(*) from {TABLENAME} where {KEYCOLUMN} = @key", new {key});
            if(existing == 1)
                connection.Execute($"update {TABLENAME} set {VALUECOLUMN} = @svalue where {KEYCOLUMN} = @key", new {svalue, key});
            else
                connection.Execute($"insert into {TABLENAME} values(@key,@svalue)", new {svalue, key});
        }
    }

}