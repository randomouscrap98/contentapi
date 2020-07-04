using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ModuleServiceConfig
    {
        public TimeSpan CleanupAge {get;set;} = TimeSpan.FromDays(2);
        public string ModuleDataConnectionString {get;set;} = "Data Source=moduledata.db"; 
    }

    public class SqliteLoadedModule : LoadedModule
    {
        public long currentUser = 0;
        public SqliteConnection dataConnection = null;
    }

    public class ModuleService : IModuleService
    {
        protected ISignaler<ModuleMessage> signaler;
        protected ILogger logger;
        protected ModuleServiceConfig config;

        protected ConcurrentDictionary<string, object> moduleLocks = new ConcurrentDictionary<string, object>();
        protected ConcurrentDictionary<string, SqliteLoadedModule> loadedModules = new ConcurrentDictionary<string, SqliteLoadedModule>();
        protected ConcurrentDictionary<long, List<ModuleMessage>> privateMessages = new ConcurrentDictionary<long, List<ModuleMessage>>();

        public ModuleService(ILogger<ModuleService> logger, ISignaler<ModuleMessage> signaler, ModuleServiceConfig config)
        { 
            this.signaler = signaler;
            this.config = config;
        }

        public void AddMessage(ModuleMessage message)
        {
            var cutoff = DateTime.Today.Subtract(config.CleanupAge); //This will be the same value for an entire day

            var messageList = privateMessages.GetOrAdd(message.receiverUid, (i) => new List<ModuleMessage>());

            lock(messageList) //This is relatively safe because I don't plan on changing this reference.
            {
                messageList.Add(message);

                var index = messageList.FindIndex(0, messageList.Count, x => x.date > cutoff); //Because of cutoff, this index will be 0 except ONE time during the day.

                if (index > 0)
                    messageList.RemoveRange(0, index + 1);
            }

            signaler.SignalItems(new[] { message });
        }

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
        protected void UpdateLoadedModule(string name, SqliteLoadedModule module)
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
        public LoadedModule UpdateModule(ModuleView module)
        {
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
            var mod = new SqliteLoadedModule();
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
            mod.script.Globals["sendmessage"] = new Action<long, string>((uid, message) =>
            {
                AddMessage(new ModuleMessage()
                {
                    senderUid = mod.currentUser,
                    receiverUid = uid,
                    message = message,
                    usersInMessage = Regex.Matches(message, @"%\d+%").Select(x => long.Parse(x.Value.Trim("%".ToCharArray()))).ToList(),
                    module = module.name,
                });
            });

            SetupDatabaseForModule(module.name);
            UpdateLoadedModule(module.name, mod);

            return mod;
        }

        public bool RemoveModule(string name)
        {
            SqliteLoadedModule removedModule = null;

            //Don't want to remove a module out from under an executing command
            lock(moduleLocks.GetOrAdd(name, s => new object()))
            {
                return loadedModules.TryRemove(name, out removedModule);
            }
        }

        public string RunCommand(string module, string command, string data, Requester requester)
        {
            SqliteLoadedModule mod = null;

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
                    DynValue res = mod.script.Call(mod.script.Globals[cmdfuncname], requester.userId, data);
                    mod.currentUser = -1;
                    return res.String;
                }
            }
        }

        public async Task<List<ModuleMessage>> ListenAsync(long lastId, Requester requester, TimeSpan maxWait, CancellationToken token)
        {
            var myMessages = privateMessages.GetOrAdd(requester.userId, (l) => new List<ModuleMessage>());

            //I HAVE TO ensure the count will be static the whole time
            lock(myMessages)
            {
                if(lastId == 0)
                    lastId = myMessages.Max(x => x.id);
                else if(lastId < 0 && myMessages.Count > 0)
                    lastId = myMessages[(int)Math.Max(0, myMessages.Count + lastId)].id - 1; //Minus 1 because we WANT that last message
            }

            Func<ModuleMessage, bool> filter = m => m.id > lastId && m.receiverUid == requester.userId;

            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                //We MUST start listening FIRST so we DON'T miss anything AT ALL (we could miss valuable signals that occur while reading initially)
                var listener = signaler.ListenAsync(requester, filter, maxWait, linkedCts.Token);

                DateTime start = DateTime.Now; //Putting this down here to minimize startup time before listen (not that this little variable really matters)
                var results = myMessages.Where(filter).ToList();

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