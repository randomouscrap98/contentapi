//using System;
//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;
//using contentapi.Views;
//using MoonSharp.Interpreter;
//
//namespace contentapi.Services
//{
//    public class LoadedModule
//    {
//        public Script script;
//        public Queue<string> debug = new Queue<string>();
//    }
//
//    public interface IModuleService
//    {
//        //void AddMessage(ModuleMessage message);
//        LoadedModule UpdateModule(ModuleView module, bool force = true);
//        bool RemoveModule(string name);
//        LoadedModule GetModule(string name);
//        string RunCommand(string module, string command, string data, Requester requester);
//        //Task<List<ModuleMessage>> ListenAsync(long lastId, Requester requester, TimeSpan maxWait, CancellationToken token);
//    }
//}