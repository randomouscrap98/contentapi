using contentapi.Views;

namespace contentapi.Module;

public interface IModuleService
{
    LoadedModule? UpdateModule(ContentView module, bool force = true);
    LoadedModule? GetModule(string name);
    bool RemoveModule(string name) ;

    string RunCommand(string module, string? arglist, long userId, long parentId = 0) ;

    /// <summary>
    /// Discover all internal subcommands and associated info and parse it into a dictionary (based on lua code) 
    /// </summary>
    /// <param name="module"></param>
    /// <returns></returns>
    public Dictionary<string, ModuleSubcommandInfo?>? ParseAllSubcommands(LoadedModule module);
}