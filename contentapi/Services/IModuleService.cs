using System.Collections.Generic;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using MoonSharp.Interpreter;

namespace contentapi.Services
{
    public class LoadedModule
    {
        public Script script {get;set;}
        public Queue<string> debug {get;set;} = new Queue<string>();

        public string currentCommand = "";
        public string currentData = "";
        public long currentUser = 0;
        public SqliteConnection dataConnection = null;
    }

    public interface IModuleService
    {
        LoadedModule UpdateModule(ModuleView module, bool force = true);
        LoadedModule GetModule(string name);
        bool RemoveModule(string name) ;

        //void AddMessage(ModuleMessageView message);

        string RunCommand(string module, string command, string data, Requester requester) ;
    }
}