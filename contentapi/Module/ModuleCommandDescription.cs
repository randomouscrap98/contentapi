namespace contentapi.Module;

/// <summary>
/// Describes a module command argument, parsed out of the argument list defined in the lua script
/// </summary>
public class ModuleArgumentInfo
{
    public string name {get;set;} = "";
    public string type {get;set;} = "";
}

/// <summary>
/// Describes the entirety of a module subcommand 
/// </summary>
public class ModuleSubcommandInfo
{
    public List<ModuleArgumentInfo?> Arguments {get;set;} = new List<ModuleArgumentInfo?>();
    public string Description {get;set;} = "";
    public string FunctionName {get;set;} = "";
}
