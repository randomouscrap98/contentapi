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
                    module = module.name,
                    createDate = DateTime.Now
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

                //Remember: all we're doing is figuring out the information available from the argument list
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
                //forcedFinal is specifically for "freeform" arguments, which MUST come at the end! It just 
                //eats up the rest of the input
                if(forcedFinal)
                    throw new InvalidOperationException("No argument can come after a 'freeform' argument!");

                //Kind of wasteful, but just easier to always start with a clean slate after ripping args out
                arglist = arglist.Trim();

                //Matcher function for generic regex matches. Useful for words, users, etc.
                Action<string, Action<Match>> genericMatch = (r, a) =>
                {
                    var regex = new Regex(r, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    var match = regex.Match(arglist);

                    //An argument of a given type must ALWAYS be a pure match.
                    if(!match.Success)
                        throw new InvalidOperationException($"Parse error in argument '{argInfo.name}', not of type '{argInfo.type}'");
                    
                    //Get rid of the argument from the remaining arglist
                    arglist = regex.Replace(arglist, "");

                    //Try to do whatever the user wanted (probably adding to existingArgs)
                    try
                    {
                        a(match);
                    }
                    catch(Exception ex)
                    {
                        throw new InvalidOperationException($"Parse error in argument '{argInfo.name}' of type '{argInfo.type}', type conversion error", ex);
                    }
                };

                //Parse arguments differently based on type
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
                List<object> scriptArgs = new List<object> { requester.userId }; //Args always includes the calling user first

                //This will NOT be null if there is a subcommands variable that is a table
                var subcommands = mod.script.Globals.Get(config.SubcommandVariable)?.Table;

                //There is a subcommand variable, which means we may need to parse the input and call
                //a different function than the default!
                if (subcommands != null)
                {
                    string subarg = "";

                    //Can only check for subcommands if there's an argument list!
                    if(arglist != null)
                    {
                        var match = Regex.Match(arglist, @"^\s*(\w+)\s*(.*)$");

                        //NOTE: Subcommand currently case sensitive!
                        //First, try to match the first word against the array. If this
                        //works, this should ALWAYS be our first go. If this DOESN'T work, we
                        //fall back to the empty subarg, which is our defined default.
                        if(match.Success && subcommands.Keys.Any(x => x.String == match.Groups[1].Value))
                        {
                            //It's OK to modify arglist because we know we're in the clear
                            subarg = match.Groups[1].Value;
                            arglist = match.Groups[2].Value.Trim();
                        }
                    }

                    var subcommand = subcommands.Get(subarg)?.Table;

                    //Ah, SOMETHING finally matched all the way! Go all in on calling this subcommand
                    if(subcommand != null)
                    {
                        //The command is either defined in the subcommand definition, or we use the default.
                        cmdfuncname = subcommand.Get(config.SubcommandFunctionKey)?.String ??
                            config.DefaultSubcommandPrepend + subarg;

                        //Now see if we're parsing arguments on behalf of the lua script, or if we're just dumping the whole line in
                        var argInfos = GetArgumentInfo(subcommand);

                        //Arguments were defined! From this point on, we're being VERY strict with parsing! This could throw exceptions!
                        if(argInfos != null)
                            ParseArgs(argInfos, arglist, scriptArgs);
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