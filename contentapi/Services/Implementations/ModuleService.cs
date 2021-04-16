using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;

namespace contentapi.Services.Implementations
{
    public class ModuleServiceConfig
    {
        public int MaxDebugSize {get;set;} = 1000;
        public TimeSpan CleanupAge {get;set;} = TimeSpan.FromDays(2);
        public string ModuleDataConnectionString {get;set;} = "Data Source=moduledata.db"; 

        //Not configured elsewhere... probably. Maybe the stuff above isn't configured elsewhere either, I haven't checked.
        public string SubcommandVariable {get;set;} = "subcommands";
        public string DefaultFunction {get;set;} = "default";
        public string DefaultSubcommandPrepend {get;set;} = "command_";
        public string SubcommandFunctionKey {get;set;} = "function";
        public string ArgumentsKey {get;set;} = "arguments";
    }

    public delegate void ModuleMessageAdder (ModuleMessageView view);

    public class ModuleService : IModuleService
    {
        protected ConcurrentDictionary<string, object> moduleLocks = new ConcurrentDictionary<string, object>();
        protected ConcurrentDictionary<string, LoadedModule> loadedModules = new ConcurrentDictionary<string, LoadedModule>();
        protected ILogger logger;
        protected ModuleServiceConfig config;
        protected ModuleMessageAdder addMessage;

        public ModuleService(ModuleServiceConfig config, ILogger<ModuleService> logger, ModuleMessageAdder addMessage)//Action<ModuleMessageView> addMessage)
        {
            this.config = config;
            this.logger = logger;
            this.addMessage = addMessage;
        }

        public LoadedModule GetModule(string name)
        {
            LoadedModule module = null;
            if(!loadedModules.TryGetValue(name, out module))
                return null;
            return module;
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
                mod.debug.Enqueue($"[{mod.currentUser}:{mod.currentFunction}|{string.Join(",", mod.currentArgs)}] {m}");

                while(mod.debug.Count > config.MaxDebugSize)
                    mod.debug.Dequeue();
            });
            mod.script.Globals["sendmessage"] = new Action<long, string>((uid, message) =>
            {
                addMessage(new ModuleMessageView()
                {
                    sendUserId = mod.currentUser,
                    receiveUserId = uid,
                    message = message,
                    module = module.name
                });
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

        /// <summary>
        /// Describes a module command argument, parsed out of the argument list defined in the lua script
        /// </summary>
        public class ModuleArgumentInfo
        {
            public string name {get;set;}
            public string type {get;set;}
        }

        /// <summary>
        /// Using the given subcommand, retrieve the parsed argument information (NOT the values, just what the argument 'is')
        /// </summary>
        /// <param name="subcommand"></param>
        /// <returns></returns>
        public List<ModuleArgumentInfo> GetArgumentInfo(Table subcommand) //, string arglist) //, List<object> existingArgs)
        {
            //Do nothing, there are no args
            if(!(subcommand != null && subcommand.Keys.Any(x => x.String == config.ArgumentsKey)))
                return null;
            
            //Find the args
            var subcmdargs = subcommand.Get(config.ArgumentsKey).Table;
            
            if(subcmdargs == null)
                return null;

            //Now we can REALLY parse the args!
            var result = new List<ModuleArgumentInfo>();

            foreach(var arg in subcmdargs.Values.Select(x => x.String))
            {
                if(string.IsNullOrWhiteSpace(arg))
                    throw new InvalidOperationException("Argument specifier was the wrong type! It needs to be a string!");

                var argparts = arg.Split("_".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                if(argparts.Length != 2)
                    throw new InvalidOperationException("Argument specifier not in the right format! name_type");

                result.Add(new ModuleArgumentInfo() { name = argparts[0], type = argparts[1]});
            }

            return result;
        }

        /// <summary>
        /// Using the given argument information, fill (as in actually mutate) the given list of argument values
        /// </summary>
        /// <param name="argumentInfos"></param>
        /// <param name="arglist"></param>
        /// <param name="existingArgs"></param>
        public void ParseArgs(List<ModuleArgumentInfo> argumentInfos, string arglist, List<object> existingArgs)
        {
            var forcedFinal = false;

            foreach(var argInfo in argumentInfos)
            {
                if(forcedFinal)
                    throw new InvalidOperationException("No argument can come after a 'freeform' argument!");

                arglist.Trim();

                Action<string, Action<Match>> genericMatch = (r, a) =>
                {
                    var regex = new Regex(r, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    var match = regex.Match(arglist);

                    if(!match.Success)
                        throw new InvalidOperationException($"Parse error in argument '{argInfo.name}', not of type '{argInfo.type}'");
                    
                    arglist = regex.Replace(arglist, "");

                    try
                    {
                        a(match);
                    }
                    catch(Exception ex)
                    {
                        throw new InvalidOperationException($"Parse error in argument '{argInfo.name}' of type '{argInfo.type}', type conversion error", ex);
                    }
                };

                switch(argInfo.type)
                {                       
                    case "user":
                        genericMatch(@"^([0-9]+)(\([^\s]+\))?", m => { existingArgs.Add(long.Parse(m.Groups[1].Value)); });
                        break;
                    case "word":
                        genericMatch(@"^([^\s]+)", m => { existingArgs.Add(m.Groups[1].Value); });
                        break;
                    case "freeform":
                        existingArgs.Add(arglist); //Just append whatever is left
                        forcedFinal = true;
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown argument type: {argInfo.type} ({argInfo.name})");
                }
            }
        }

        public string RunCommand(string module, string arglist, Requester requester)
        {
            LoadedModule mod = null;

            if(!loadedModules.TryGetValue(module, out mod))
                throw new BadRequestException($"No module with name {module}");

            arglist = arglist?.Trim();

            lock(moduleLocks.GetOrAdd(module, s => new object()))
            {
                //By DEFAULT, we call the default function with whatever is leftover in the arglist
                var cmdfuncname = config.DefaultFunction;
                List<object> scriptArgs = new List<object> { requester.userId };

                //Func<DynValue> callScript = () => mod.script.Call(mod.script.Globals[cmdfuncname], requester.userId, arglist);

                //There is a subcommand variable and we passed args which could be read as a subcommand (don't know if it's the right format or anything)
                if (arglist != null && mod.script.Globals.Keys.Any(x => x.String == config.SubcommandVariable))
                {
                    var subcommands = mod.script.Globals.Get(config.SubcommandVariable).Table;
                    var match = Regex.Match(arglist, @"^\s*(\w+)\s*(.*)$");

                    //The subcommands is in the right format and our arglist indicates we have the ability to get a subcommand
                    if(subcommands != null && match.Success)
                    {
                        var subarg = match.Groups[1].Value;
                        arglist = match.Groups[2].Value.Trim();

                        //NOTE: Currently case sensitive!
                        //There is a subcommand defined for the subcommand we pulled from the arglist. 
                        if (subcommands.Keys.Any(x => x.String == subarg))
                        {
                            var subcommand = subcommands.Get(subarg).Table;

                            //The function to call at this point is either the default subcommand naming scheme or the user's requested function
                            if (subcommand != null && subcommand.Keys.Any(x => x.String == config.SubcommandFunctionKey))
                                cmdfuncname = subcommand.Get(config.SubcommandFunctionKey).String;
                            else
                                cmdfuncname = config.DefaultSubcommandPrepend + subarg;

                            //Check args and parse here, altering "callScript"
                            var argInfos = GetArgumentInfo(subcommand);

                            if(argInfos != null)
                                ParseArgs(argInfos, arglist, scriptArgs);
                        }
                    }
                }

                if(!mod.script.Globals.Keys.Any(x => x.String == cmdfuncname))
                    throw new BadRequestException($"Command function '{cmdfuncname}' not found in module {module}");

                //Oops, didn't fill up the arglist with anything! Careful, this is dangerous!
                if(scriptArgs.Count == 1)
                    scriptArgs.Add(arglist);

                using(mod.dataConnection = new SqliteConnection(config.ModuleDataConnectionString))
                {
                    mod.dataConnection.Open();
                    mod.currentUser = requester.userId;
                    mod.currentFunction = cmdfuncname;
                    mod.currentArgs = arglist;
                    DynValue res = mod.script.Call(mod.script.Globals[cmdfuncname], scriptArgs.ToArray());
                    return res.String;
                }
            }
        }
    }
}