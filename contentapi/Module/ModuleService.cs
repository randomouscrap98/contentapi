using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using contentapi.data.Views;
using Microsoft.Data.Sqlite;
using MoonSharp.Interpreter;
using contentapi.data;
using contentapi.Main;
using System.Runtime.ExceptionServices;

namespace contentapi.Module;

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
    public string DescriptionKey {get;set;} = "description";
}

//public delegate void ModuleMessageAdder (MessageView view, long requester);

public class ModuleService : IModuleService
{
    protected ConcurrentDictionary<string, object> moduleLocks = new ConcurrentDictionary<string, object>();
    protected ConcurrentDictionary<string, LoadedModule> loadedModules = new ConcurrentDictionary<string, LoadedModule>();
    protected ILogger logger;
    protected ModuleServiceConfig config;
    //protected ModuleMessageAdder addMessage;
    protected IDbServicesFactory dbFactory;

    public ModuleService(ModuleServiceConfig config, ILogger<ModuleService> logger, //ModuleMessageAdder addMessage,
        IDbServicesFactory factory)
    {
        this.config = config;
        this.logger = logger;
        //this.addMessage = addMessage;
        this.dbFactory = factory;
    }

    public LoadedModule? GetModule(string name)
    {
        LoadedModule? module = null;
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
    public LoadedModule? UpdateModule(ContentView module, bool force = true)
    {
        if(!force && loadedModules.ContainsKey(module.name) && loadedModules[module.name].lastRevisionId == module.lastRevisionId)
            return null;
        //if(!force && loadedModules.ContainsKey(module.name))
        //    return null;

        var getValue = new Func<string, string?>((s) => 
        {
            if(module.values.ContainsKey(s)) 
                return module.values[s].ToString();
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
        mod.script.DoString(module.text);     //This could take a LONG time.
        mod.lastRevisionId = module.lastRevisionId;
        mod.contentId = module.id;

        var getData = new Func<string, Dictionary<string, string?>>((k) =>
        {
            var command = mod.dataConnection?.CreateCommand() ?? throw new InvalidOperationException("No connection to module storage!");
            command.CommandText = $"SELECT key, value FROM {module.name} WHERE key LIKE $key";
            command.Parameters.AddWithValue("$key", k);
            var result = new Dictionary<string, string?>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    result[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
            return result;
        });
        
        var sendRawMessage = new Action<long, long, string>((uid, room, message) =>
        {
            using var writer = dbFactory.CreateWriter();
            var realMessage = new MessageView()
            {
                createUserId = mod.currentUserId,
                contentId = room,
                receiveUserId = uid,
                text = message,
                module = module.name,
                createDate = DateTime.Now
            }; //, mod.currentUserId);
            try {
                writer.WriteAsync(realMessage, mod.currentUserId).Wait();
            }
            catch(AggregateException ex) {
                ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            }
            //addMessage(
        });

        mod.script.Globals["getdata"] = new Func<string, string?>((k) =>
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
            var command = mod.dataConnection?.CreateCommand() ?? throw new InvalidOperationException("No connection to module storage!");
            var vtext = "$value";

            command.Parameters.AddWithValue("$key", k);

            if(v != null)
                command.Parameters.AddWithValue("$value", v);
            else
                vtext = "NULL";

            command.CommandText = $"INSERT OR REPLACE INTO {module.name} (key, value) values($key, {vtext})";
            command.ExecuteNonQuery();
        });
        mod.script.Globals["getvalue"] = getValue;
        mod.script.Globals["getvaluenum"] = getValueNum;
        mod.script.Globals["prntdbg"] = new Action<string>((m) => 
        {
            //mod.debug.Enqueue($"[{mod.currentRequester.userId}:{mod.currentFunction}|{string.Join(",", mod.currentArgs)}] {m} ({DateTime.Now})");
            mod.debug.Enqueue($"[{mod.currentUserId}:{mod.currentFunction}|{string.Join(",", mod.currentArgs)}] {m} ({DateTime.Now})");

            while(mod.debug.Count > config.MaxDebugSize)
                mod.debug.Dequeue();
        });
        //mod.script.Globals["sendrawmessage"] = sendRawMessage;
        mod.script.Globals["usermessage"] = new Action<long, string>((uid, message) => sendRawMessage(uid, mod.currentParentId, message)); //sendRawMessage(uid, 0, message));
        mod.script.Globals["broadcastmessage"] = new Action<string>((message) => sendRawMessage(0, mod.currentParentId, message));

        var pluralize = new Func<int, string, string>((i, s) => s + (i == 1 ? "" : "s"));
        var agoize = new Func<double, string, string>((d,s) => (int)d + " " + pluralize((int)d, s) + " ago");

        mod.script.Globals["pluralize"] = pluralize;
        mod.script.Globals["gettimestamp"] = new Func<string>(() => DateTime.Now.ToString());
        mod.script.Globals["timesincetimestamp"] = new Func<string, string>((t) =>
        {
            TimeSpan diff = DateTime.Now - Convert.ToDateTime(t);

            if(diff.TotalSeconds < 10)
                return "Moments ago";
            else if(diff.TotalSeconds < 60)
                return agoize(diff.TotalSeconds, "second");
            else if(diff.TotalMinutes < 60)
                return agoize(diff.TotalMinutes, "minute");
            else if(diff.TotalHours < 24)
                return agoize(diff.TotalHours, "hour");
            else if(diff.TotalDays < 30)
                return agoize(diff.TotalDays, "day");
            else if(diff.TotalDays < 365)
                return agoize(diff.TotalDays / 30, "month");
            else
                return agoize(diff.TotalDays / 365, "year");
        });
        mod.script.Globals["b64encode"] = new Func<string, string?>(s =>
        {
            if(s == null) return null;
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(s);
            return System.Convert.ToBase64String(plainTextBytes).Replace('/', '!');
        });
        mod.script.Globals["b64decode"] = new Func<string, string?>(s =>
        {
            if(s == null) return null;
            var base64EncodedBytes = System.Convert.FromBase64String(s.Replace('!', '/'));
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        });

        mod.script.Globals["getbroadcastid"] = new Func<long>(() => mod.currentParentId);

        SetupDatabaseForModule(module.name);
        mod.subcommands = ParseAllSubcommands(mod) ?? new Dictionary<string, ModuleSubcommandInfo?>();
        
        UpdateLoadedModule(module.name, mod);

        return mod;
    }

    public bool RemoveModule(string name)
    {
        LoadedModule? removedModule = null;

        //Don't want to remove a module out from under an executing command
        lock(moduleLocks.GetOrAdd(name, s => new object()))
        {
            return loadedModules.TryRemove(name, out removedModule);
        }
    }

    /// <summary>
    /// Using the given subcommand, retrieve the parsed subcommand information (such as arguments. NOT the values, just what the argument 'is')
    /// </summary>
    /// <param name="subcommand"></param>
    /// <returns></returns>
    public ModuleSubcommandInfo? ParseSubcommandInfo(Table subcommand)
    {
        if(subcommand == null)
            return null;

        var result = new ModuleSubcommandInfo()
        {
            Arguments = new List<ModuleArgumentInfo?>(),
            Description = subcommand.Get(config.DescriptionKey)?.String,
            FunctionName = subcommand.Get(config.SubcommandFunctionKey)?.String
        };

        //Find the args
        var subcmdargs = subcommand.Get(config.ArgumentsKey).Table;
        
        //Now we can REALLY parse the args!
        if(subcmdargs != null)
        {
            foreach (var arg in subcmdargs.Values.Select(x => x.String))
            {
                if (string.IsNullOrWhiteSpace(arg))
                    throw new InvalidOperationException("Argument specifier was the wrong type! It needs to be a string!");

                var argparts = arg.Split("_".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                if (argparts.Length != 2)
                    throw new InvalidOperationException("Argument specifier not in the right format! name_type");

                //Remember: all we're doing is figuring out the information available from the argument list
                result.Arguments.Add(new ModuleArgumentInfo() { name = argparts[0], type = argparts[1] });
            }
        }

        return result;
    }

    /// <summary>
    /// Parse a single subcommand from a loaded module
    /// </summary>
    /// <param name="module"></param>
    /// <param name="subkey"></param>
    /// <returns></returns>
    public ModuleSubcommandInfo? ParseSubcommandInfo(LoadedModule module, string subkey)
    {
        var subcommands = module.script.Globals.Get(config.SubcommandVariable)?.Table;

        //No point continuing if there's no subcommands
        if (subcommands == null)
            return null;

        var table = subcommands.Get(subkey)?.Table;

        if(table == null)
        {
            logger.LogWarning($"Key {subkey} in subcommands table didn't map to a value!");
            return null;
        }

        //This is interesting, because we want to return SOME module info if there was a valid subkey...
        var subcommand = ParseSubcommandInfo(table) ?? new ModuleSubcommandInfo();
        subcommand.FunctionName ??= config.DefaultSubcommandPrepend + subkey;

        return subcommand;
    }

    /// <summary>
    /// Retrieve a pre-parsed list of all subcommands for the given module
    /// </summary>
    /// <param name="module"></param>
    /// <returns></returns>
    public Dictionary<string, ModuleSubcommandInfo?>? ParseAllSubcommands(LoadedModule module)
    {
        if(module == null)
            return null;

        var subcommands = module.script.Globals.Get(config.SubcommandVariable)?.Table;

        //There is a subcommand variable, which means we may need to parse the input and call
        //a different function than the default!
        if (subcommands == null)
            return null;
        
        var result = new Dictionary<string, ModuleSubcommandInfo?>();

        foreach(var key in subcommands.Keys)
        {
            var subname = key.String;

            if(subname == null)
            {
                logger.LogWarning($"Key {key} in subcommands table wasn't string value!");
                continue;
            }

            result.Add(subname, ParseSubcommandInfo(module, subname));
        }

        return result;
    }

    /// <summary>
    /// Using the given argument information, fill (as in actually mutate) the given list of argument values
    /// </summary>
    /// <param name="argumentInfos"></param>
    /// <param name="arglist"></param>
    /// <param name="existingArgs"></param>
    public void ParseArgs(ModuleSubcommandInfo subcommandInfo, string arglist, List<object> existingArgs)
    {
        using var search = dbFactory.CreateSearch();
        var forcedFinal = false;

        foreach(var argInfo in subcommandInfo.Arguments)
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
                    throw new RequestException($"Parse error in argument '{argInfo?.name}', not of type '{argInfo?.type}'");
                
                //Get rid of the argument from the remaining arglist
                arglist = regex.Replace(arglist, "");

                //Try to do whatever the user wanted (probably adding to existingArgs)
                try
                {
                    a(match);
                }
                catch(Exception ex)
                {
                    throw new RequestException($"{ex.Message} (Parse error in argument '{argInfo?.name}' of type '{argInfo?.type}')", ex);
                }
            };

            //Parse arguments differently based on type
            switch(argInfo?.type)
            {                       
                case "user":
                    genericMatch(@"^([0-9]+)(\([^\s]+\))?", m => { 
                        //Check if user exists!
                        var uid = long.Parse(m.Groups[1].Value);
                        //Yes this is BLOCKING, this entire module system is blocking because lua/etc
                        //var users = userSource.SimpleSearchAsync(new UserSearch() { Ids = new List<long>{uid}}).Result;
                        try {
                            var user = search.GetById<UserView>(RequestType.user, uid);
                        }
                        catch(NotFoundException ex) {
                            throw new RequestException($"User not found: {uid}", ex);
                        }
                        existingArgs.Add(uid);
                    });
                    break;
                case "int":
                    // This will throw an exception on error, not telling the user much
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
                    throw new InvalidOperationException($"Unknown argument type: {argInfo?.type} ({argInfo?.name})");
            }
        }
    }

    /// <summary>
    /// Run the given argument list (as taken directly from a request) with the given module for the given requester. Parses the arglist, 
    /// finds the module, runs the command and returns the output. Things like sending messages to other users is also performed, but
    /// against the database and in the background  
    /// </summary>
    /// <param name="module"></param>
    /// <param name="arglist"></param>
    /// <param name="requester"></param>
    /// <returns></returns>
    public string RunCommand(string module, string? arglist, long userId, long parentId = 0)
    {
        LoadedModule? mod = GetModule(module);

        if(mod == null)
            throw new RequestException($"No module with name {module}");

        arglist = arglist?.Trim();

        //By DEFAULT, we call the default function with whatever is leftover in the arglist
        var cmdfuncname = config.DefaultFunction;
        //List<object> scriptArgs = new List<object> { requester.userId }; //Args always includes the calling user first
        List<object> scriptArgs = new List<object> { userId }; //Args always includes the calling user first

        //Can only check for subcommands if there's an argument list!
        if(arglist != null)
        {
            var match = Regex.Match(arglist, @"^\s*(\w+)\s*(.*)$");

            //NOTE: Subcommand currently case sensitive!
            if(match.Success) 
            {
                var newArglist = match.Groups[2].Value.Trim();
                ModuleSubcommandInfo? subcommandInfo = null;
                mod.subcommands.TryGetValue(match.Groups[1].Value, out subcommandInfo); 

                //Special re-check: sometimes, we can have commands that have NO subcommand name, or the "blank" subcommand. Try that one.
                if(subcommandInfo == null) 
                {
                    newArglist = arglist; //undo the parsing
                    mod.subcommands.TryGetValue("", out subcommandInfo);
                }

                //There is a defined subcommand, which means we may need to parse the input and call
                //a different function than the default!
                if (subcommandInfo != null) //subcommandExists == true) //subcommandInfo != null)
                {
                    arglist = newArglist;
                    cmdfuncname = subcommandInfo.FunctionName;

                    //Arguments were defined! From this point on, we're being VERY strict with parsing! This could throw exceptions!
                    if (subcommandInfo.Arguments != null)
                        ParseArgs(subcommandInfo, arglist, scriptArgs);
                }
            }

            //Oops, didn't fill up the arglist with anything! Careful, this is dangerous!
            if(scriptArgs.Count == 1)
                scriptArgs.Add(arglist);
        }

        if(!mod.script.Globals.Keys.Any(x => x.String == cmdfuncname))
            throw new RequestException($"Command function '{cmdfuncname}' not found in module {module}");

        //We lock so nobody else can run commands while we're running them. This guarantees thread safety 
        //within the modules so they don't have to worry about it.
        lock(moduleLocks.GetOrAdd(module, s => new object()))
        {
            using(mod.dataConnection = new SqliteConnection(config.ModuleDataConnectionString))
            {
                mod.dataConnection.Open();
                mod.currentUserId = userId;
                mod.currentFunction = cmdfuncname;
                mod.currentParentId = parentId;
                mod.currentArgs = arglist;
                DynValue res = mod.script.Call(mod.script.Globals[cmdfuncname], scriptArgs.ToArray());
                return res.String;
            }
        }
    }
}