using System;
using System.Collections.Generic;
using contentapi.Services;
using contentapi.Views;

namespace contentapi.test.Implementations
{
    public class FakeModuleService : IModuleService
    {
        public List<ModuleMessageView> messages = new List<ModuleMessageView>();

        public void AddMessage(ModuleMessageView message)
        {
            messages.Add(message);
        }

        public LoadedModule GetModule(string name) { return null; }

        public Dictionary<string, ModuleSubcommandInfo> ParseAllSubcommands(LoadedModule module) { return null; }

        public bool RemoveModule(string name) { return true; }
        public string RunCommand(string module, string arglist, Requester requester) { return "Not implemented"; }
        public LoadedModule UpdateModule(ModuleView module, bool force) { return null; }
    }
}