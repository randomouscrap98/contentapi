using Microsoft.Data.Sqlite;
using MoonSharp.Interpreter;

namespace contentapi.Module;

/// <summary>
/// All data related to a LOADED module (as in, one whose code has been pre-parsed, has its own database, etc) 
/// </summary>
public class LoadedModule
{
    public Script script {get;set;} = new Script();
    public Dictionary<string, ModuleSubcommandInfo?> subcommands {get;set;} = new Dictionary<string, ModuleSubcommandInfo?>();
    //= new Dictionary<string, ModuleSubcommandInfo>();
    public Queue<string> debug {get;set;} = new Queue<string>();

    public string currentFunction = "";
    public string? currentArgs = null;
    public long currentUserId = 0;
    public long currentParentId = 0;
    public SqliteConnection dataConnection = new SqliteConnection(":memory:");
}
