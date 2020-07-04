using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Views;
using MoonSharp.Interpreter;

namespace contentapi.Services
{
    public class ModuleMessage : IIdView
    {
        private static long GlobalId = 0;

        public DateTime date {get;set;} = DateTime.Now;
        public long id {get;set;} = Interlocked.Increment(ref GlobalId);
        public string message {get;set;}
        public List<long> usersInMessage {get;set;} = new List<long>();
        public string module {get;set;}
        public long receiverUid {get;set;} = -1;
        public long senderUid {get;set;} = -1;
    }

    public class LoadedModule
    {
        public Script script;
    }

    public interface IModuleService
    {
        void AddMessage(ModuleMessage message);
        LoadedModule UpdateModule(ModuleView module);
        bool RemoveModule(string name);
        string RunCommand(string module, string command, string data, Requester requester);
        Task<List<ModuleMessage>> ListenAsync(long lastId, Requester requester, TimeSpan maxWait, CancellationToken token);
    }
}