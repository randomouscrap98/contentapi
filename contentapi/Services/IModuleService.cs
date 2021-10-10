using System.Collections.Generic;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using MoonSharp.Interpreter;

namespace contentapi.Services
{
    /// <summary>
    /// All data related to a LOADED module (as in, one whose code has been pre-parsed, has its own database, etc) 
    /// </summary>
    public class LoadedModule
    {
        public Script script {get;set;}
        public Dictionary<string, ModuleSubcommandInfo> subcommands {get;set;}
        public Queue<string> debug {get;set;} = new Queue<string>();

        public string currentFunction = "";
        public string currentArgs = "";
        public long currentUser = 0;
        public SqliteConnection dataConnection = null;
    }

    /// <summary>
    /// Describes a module command argument, parsed out of the argument list defined in the lua script
    /// </summary>
    public class ModuleArgumentInfo
    {
        public string name {get;set;}
        public string type {get;set;}
    }

    /// <summary>
    /// Describes the entirety of a module subcommand 
    /// </summary>
    public class ModuleSubcommandInfo
    {
        public List<ModuleArgumentInfo> Arguments {get;set;} = null;
        public string Description {get;set;}
        public string FunctionName {get;set;}
    }


    public interface IModuleService
    {
        LoadedModule UpdateModule(ModuleView module, bool force = true);
        LoadedModule GetModule(string name);
        bool RemoveModule(string name) ;

        string RunCommand(string module, string arglist, Requester requester) ;

        /// <summary>
        /// Discover all internal subcommands and associated info and parse it into a dictionary (based on lua code) 
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public Dictionary<string, ModuleSubcommandInfo> ParseAllSubcommands(LoadedModule module);
    }
}