using contentapi.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using contentapi.Services.Extensions;
using contentapi.Services.Constants;
using System.Text.RegularExpressions;
using Randomous.EntitySystem;
using System;
using Microsoft.Data.Sqlite;
using MoonSharp.Interpreter;
using System.Collections.Concurrent;
using System.Linq;

namespace contentapi.Services.Implementations
{
    public class ModuleServiceConfig
    {
        public int MaxDebugSize {get;set;} = 1000;
        public TimeSpan CleanupAge {get;set;} = TimeSpan.FromDays(2);
        public string ModuleDataConnectionString {get;set;} = "Data Source=moduledata.db"; 
    }

    public class LoadedModule
    {
        public Script script {get;set;}
        public Queue<string> debug {get;set;} = new Queue<string>();

        public string currentCommand = "";
        public string currentData = "";
        public long currentUser = 0;
        public SqliteConnection dataConnection = null;
    }

    public class ModuleViewService : BaseEntityViewService<ModuleView, ModuleSearch>
    {
        //protected IModuleService service;
        //protected ISignaler<ModuleMessage> signaler;
        protected ModuleServiceConfig config;
        protected ModuleMessageViewService moduleMessageService;

        public ModuleViewService(ILogger<ModuleViewService> logger, ViewServicePack services, ModuleViewSource converter,
            ModuleServiceConfig config, ModuleMessageViewService moduleMessageService/*, IModuleService service*/) :base(services, logger, converter) 
        { 
            this.config = config;
            this.moduleMessageService = moduleMessageService;
            //this.service = service;
        }

        public override string EntityType => Keys.ModuleType;

        public async Task SetupAsync()
        {
            var modules = await SearchAsync(new ModuleSearch(), new Requester() { system = true });
            foreach(var module in modules)
                UpdateModule(module, false);
        }

        public override async Task<ModuleView> CleanViewGeneralAsync(ModuleView view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);

            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Only supers can create modules!");

            if(!Regex.IsMatch(view.name, "^[a-z0-9_]+$"))
                throw new BadRequestException("Module name can only be lowercase letters, numbers, and _");

            var found = await FindByNameAsync(view.name);

            if(found != null && found.Entity.id != view.id)
                throw new BadRequestException($"A module with name '{view.name}' already exists!");

            return view;
        }

        public override async Task<EntityPackage> DeleteCheckAsync(long entityId, Requester requester) 
        {
            var result = await base.DeleteCheckAsync(entityId, requester);

            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Only supers can delete modules!");
            
            return result;
        }

        public override async Task<ModuleView> WriteAsync(ModuleView view, Requester requester)
        {
            var result = await base.WriteAsync(view, requester);
            UpdateModule(result);
            return result;
        }

        public override async Task<ModuleView> DeleteAsync(long entityId, Requester requester)
        {
            var result = await base.DeleteAsync(entityId, requester);
            RemoveModule(result.name);
            return result;
        }

        public override Task<List<ModuleView>> PreparedSearchAsync(ModuleSearch search, Requester requester)
        {
            //NO permissions check! All modules are readable!
            return converter.SimpleSearchAsync(search);
        }

        //protected

        protected ConcurrentDictionary<string, object> moduleLocks = new ConcurrentDictionary<string, object>();
        protected ConcurrentDictionary<string, LoadedModule> loadedModules = new ConcurrentDictionary<string, LoadedModule>();
        protected ConcurrentDictionary<long, List<ModuleMessageView>> privateMessages = new ConcurrentDictionary<long, List<ModuleMessageView>>();

        public LoadedModule GetModule(string name)
        {
            LoadedModule module = null;
            if(!loadedModules.TryGetValue(name, out module))
                return null;
            return module;
        }

        //public void AddMessage(ModuleMessage message)
        //{
        //    var cutoff = DateTime.Today.Subtract(config.CleanupAge); //This will be the same value for an entire day

        //    var messageList = privateMessages.GetOrAdd(message.receiverUid, (i) => new List<ModuleMessage>());

        //    lock(messageList) //This is relatively safe because I don't plan on changing this reference.
        //    {
        //        messageList.Add(message);

        //        var index = messageList.FindIndex(0, messageList.Count, x => x.date > cutoff); //Because of cutoff, this index will be 0 except ONE time during the day.

        //        if (index > 0)
        //            messageList.RemoveRange(0, index + 1);
        //    }

        //    signaler.SignalItems(new[] { message });
        //}

        /// <summary>
        /// Setup the DATA database for the given module
        /// </summary>
        /// <param name="module"></param>
        protected void SetupDatabaseForModule(string module)
        {
            //For performance, need to create table now in case it doesn't exist
            using(var con = new SqliteConnection(config.ModuleDataConnectionString))
            {
                con.Open();
                var command = con.CreateCommand();
                command.CommandText = $"CREATE TABLE IF NOT EXISTS {module} (key TEXT PRIMARY KEY, value TEXT)";
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Assuming we have an already-setup loaded module, update the in-memory cache
        /// </summary>
        /// <param name="name"></param>
        /// <param name="module"></param>
        protected void UpdateLoadedModule(string name, LoadedModule module)
        {
            lock(moduleLocks.GetOrAdd(name, s => new object()))
            {
                //loadedModules is a concurrent dictionary and thus adding is thread-safe
                loadedModules[name] = module;
            }
        }

        /// <summary>
        /// Update our modules with the given module view (parses code, sets up data, etc.)
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public LoadedModule UpdateModule(ModuleView module, bool force = true)
        {
            if(!force && loadedModules.ContainsKey(module.name))
                return null;

            var getValue = new Func<string, string>((s) => 
            {
                if(module.values.ContainsKey(s)) 
                    return module.values[s];
                else
                    return null;
            });

            var getValueNum = new Func<string, long>((s) =>
            {
                long result = 0;
                long.TryParse(getValue(s), out result);
                return result;
            });

            //no matter if it's an update or whatever, have to just rebuild the module
            var mod = new LoadedModule();
            mod.script = new Script();
            mod.script.DoString(module.code);     //This could take a LONG time.

            var getData = new Func<string, Dictionary<string, string>>((k) =>
            {
                var command = mod.dataConnection.CreateCommand();
                command.CommandText = $"SELECT key, value FROM {module.name} WHERE key LIKE $key";
                command.Parameters.AddWithValue("$key", k);
                var result = new Dictionary<string, string>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        result[reader.GetString(0)] = reader.GetString(1);
                }
                return result;
            });

            mod.script.Globals["getdata"] = new Func<string, string>((k) =>
            {
                var result = getData(k);
                if(result.ContainsKey(k))
                    return result[k];
                else
                    return null;
            });
            mod.script.Globals["getalldata"] = getData;
            mod.script.Globals["setdata"] = new Action<string, string>((k,v) =>
            {
                var command = mod.dataConnection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO {module.name} (key, value) values($key, $value)";
                command.Parameters.AddWithValue("$key", k);
                command.Parameters.AddWithValue("$value", v);
                command.ExecuteNonQuery();
            });
            mod.script.Globals["getvalue"] = getValue;
            mod.script.Globals["getvaluenum"] = getValueNum;
            mod.script.Globals["prntdbg"] = new Action<string>((m) => 
            {
                mod.debug.Enqueue($"[{mod.currentUser}:{mod.currentCommand}|{mod.currentData}] {m}");

                while(mod.debug.Count > config.MaxDebugSize)
                    mod.debug.Dequeue();
            });
            mod.script.Globals["sendmessage"] = new Action<long, string>((uid, message) =>
            {
                moduleMessageService.AddMessageAsync(new ModuleMessageView()
                {
                    senderUid = mod.currentUser,
                    receiverUid = uid,
                    message = message,
                    module = module.name
                }).Wait();
            });

            SetupDatabaseForModule(module.name);
            UpdateLoadedModule(module.name, mod);

            return mod;
        }

        public bool RemoveModule(string name)
        {
            LoadedModule removedModule = null;

            //Don't want to remove a module out from under an executing command
            lock(moduleLocks.GetOrAdd(name, s => new object()))
            {
                return loadedModules.TryRemove(name, out removedModule);
            }
        }

        public string RunCommand(string module, string command, string data, Requester requester)
        {
            LoadedModule mod = null;

            if(!loadedModules.TryGetValue(module, out mod))
                throw new BadRequestException($"No module with name {module}");
            
            var cmdfuncname = $"command_{command}";

            lock(moduleLocks.GetOrAdd(module, s => new object()))
            {
                if(!mod.script.Globals.Keys.Any(x => x.String == cmdfuncname))
                    throw new BadRequestException($"No command '{command}' in module {module}");

                using(mod.dataConnection = new SqliteConnection(config.ModuleDataConnectionString))
                {
                    mod.dataConnection.Open();
                    mod.currentUser = requester.userId;
                    mod.currentCommand = command;
                    mod.currentData = data;
                    DynValue res = mod.script.Call(mod.script.Globals[cmdfuncname], requester.userId, data);
                    //mod.currentUser = -1;
                    //mod.currentCommand = "";
                    //mod.currentData = "";
                    return res.String;
                }
            }
        }

        //public async Task<List<ModuleMessage>> ListenAsync(long lastId, Requester requester, TimeSpan maxWait, CancellationToken token)
        //{
        //    var myMessages = privateMessages.GetOrAdd(requester.userId, (l) => new List<ModuleMessage>());

        //    //I HAVE TO ensure the count will be static the whole time
        //    lock(myMessages)
        //    {
        //        if(myMessages.Count > 0)
        //        {
        //            if (lastId == 0)
        //                lastId = myMessages.Max(x => x.id);
        //            else if (lastId < 0)
        //                lastId = myMessages[(int)Math.Max(0, myMessages.Count + lastId)].id - 1; //Minus 1 because we WANT that last message
        //        }
        //    }

        //    Func<ModuleMessage, bool> filter = m => m.id > lastId && m.receiverUid == requester.userId;

        //    using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token))
        //    {
        //        //We MUST start listening FIRST so we DON'T miss anything AT ALL (we could miss valuable signals that occur while reading initially)
        //        var listener = signaler.ListenAsync(requester, filter, maxWait, linkedCts.Token);

        //        DateTime start = DateTime.Now; //Putting this down here to minimize startup time before listen (not that this little variable really matters)
        //        var results = myMessages.Where(filter).ToList();

        //        if (results.Count > 0)
        //        {
        //            linkedCts.Cancel();

        //            try
        //            {
        //                //Yes, we are so confident that we don't even worry about waiting properly
        //                await listener;
        //            }
        //            catch(OperationCanceledException) {} //This is expected

        //            return results;
        //        }
        //        else
        //        {
        //            return (await listener).Cast<ModuleMessage>().ToList();
        //        }
        //    }
        //}
    }
}