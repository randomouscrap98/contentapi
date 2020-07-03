using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ModuleMessage
    {
        private static long GlobalId = 0;

        public DateTime date {get;set;} = DateTime.Now;
        public long id = Interlocked.Increment(ref GlobalId);
        public string message {get;set;}
        public string module {get;set;}
        public long receiverUid = -1;
    }

    public class LoadedModule
    {
        public Script script;
    }

    public class ModuleServiceConfig
    {
        public TimeSpan CleanupAge = TimeSpan.FromDays(3);
    }

    public class ModuleService
    {
        protected ISignaler<ModuleMessage> signaler;
        protected ILogger logger;
        protected ModuleServiceConfig config;

        protected Dictionary<string, LoadedModule> loadedModules = new Dictionary<string, LoadedModule>();
        protected List<ModuleMessage> privateMessages = new List<ModuleMessage>();
        protected readonly object messageLock = new object();
        protected readonly object moduleLock = new object();

        public ModuleService(ILogger<ModuleService> logger, ISignaler<ModuleMessage> signaler, ModuleServiceConfig config)
        { 
            this.signaler = signaler;
            this.config = config;
        }

        public void AddMessage(ModuleMessage message)
        {
            lock(messageLock)
            {
                privateMessages.Add(message);

                var cutoff = DateTime.Today.Subtract(config.CleanupAge); //This will be the same value for an entire day
                var index = privateMessages.FindIndex(0, privateMessages.Count, x => x.date > cutoff); //As such, this index will be 0 except ONE time during the day.

                if(index > 0)
                    privateMessages = privateMessages.Skip(index).ToList();

                signaler.SignalItems(new[] { message });
            }
        }

        public LoadedModule UpdateModule(ModuleView module)
        {
            //no matter if it's an update or whatever, have to just rebuild the module
            var mod = new LoadedModule();
            mod.script = new Script();
            mod.script.DoString(module.code);     //This could take a LONG time.
            mod.script.Globals["getdata"] = null; //modules[name].saveData;
            mod.script.Globals["setdata"] = null; //modules[name].saveData;
            mod.script.Globals["sendmessage"] = new Action<long, string>((uid, message) =>
            {
                AddMessage(new ModuleMessage()
                {
                    receiverUid = uid,
                    message = message,
                    module = module.name,
                });
            });

            lock(moduleLock)
            {
                loadedModules[module.name] = mod;
            }

            return mod;
        }

        public bool RemoveModule(string name)
        {
            lock(moduleLock)
            {
                if(loadedModules.ContainsKey(name))
                {
                    loadedModules.Remove(name);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public async Task<List<ModuleMessage>> ListenAsync(long lastId, Requester requester, TimeSpan maxWait, CancellationToken token)
        {
            Func<ModuleMessage, bool> filter = m => m.id > lastId && m.receiverUid == requester.userId;

            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                //We MUST start listening FIRST so we DON'T miss anything AT ALL (we could miss valuable signals that occur while reading initially)
                var listener = signaler.ListenAsync(requester, filter, maxWait, linkedCts.Token);

                DateTime start = DateTime.Now; //Putting this down here to minimize startup time before listen (not that this little variable really matters)
                var results = privateMessages.Where(filter).ToList();

                if (results.Count > 0)
                {
                    linkedCts.Cancel();

                    try
                    {
                        //Yes, we are so confident that we don't even worry about waiting properly
                        await listener;
                    }
                    catch(OperationCanceledException) {} //This is expected

                    return results;
                }
                else
                {
                    return (await listener).Cast<ModuleMessage>().ToList();
                }
            }
        }
    }
}