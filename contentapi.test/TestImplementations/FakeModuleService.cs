using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Views;

namespace contentapi.test.Implementations
{
    public class FakeModuleService : IModuleService
    {
        public List<ModuleMessage> messages = new List<ModuleMessage>();

        public void AddMessage(ModuleMessage message)
        {
            messages.Add(message);
        }

        public Task<List<ModuleMessage>> ListenAsync(long lastId, Requester requester, TimeSpan maxWait, CancellationToken token)
        {
            return Task.FromResult(messages.Where(x => x.id > lastId && x.receiverUid == requester.userId).ToList());
        }

        public bool RemoveModule(string name) { return true; }
        public string RunCommand(string module, string command, string data, Requester requester) { return "Not implemented"; }
        public LoadedModule UpdateModule(ModuleView module) { return null; }
    }
}