using contentapi.data;
using contentapi.data.Views;
using contentapi.Search;
using contentapi.Utilities;

namespace contentapi.Module;

public static class ModuleServiceExtensions
{
    /// <summary>
    /// Get a single module by name with only the fields required for system blah blah blah
    /// </summary>
    /// <param name="search"></param>
    /// <param name="name"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public static async Task<ContentView?> GetModuleForSystemByNameAsync(this IGenericSearch search, string name, long userId = -1)
    {
        var request = new SearchRequest()
        {
            type = "content",
            fields = "~votes,permissions,keywords,values",
            query = "name = @name and contentType = @type and !notdeleted()"
        }; 
        var values = new Dictionary<string, object> {
            { "name", name },
            { "type", Db.InternalContentType.module }
        };

        List<ContentView> modules = new List<ContentView>();

        if(userId < 0)
            modules = await search.SearchSingleTypeUnrestricted<ContentView>(request, values);
        else
            modules = await search.SearchSingleType<ContentView>(userId, request, values);

        return modules.FirstOrDefault(x => x.name == name && x.contentType == Db.InternalContentType.module); 
    }

    /// <summary>
    /// Query for module by name, update the loaded module, and return the loaded module
    /// </summary>
    /// <param name="service"></param>
    /// <param name="search"></param>
    /// <param name="name"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public static async Task<LoadedModule> RefreshModuleAsync(this IModuleService service, IGenericSearch search, string name, long userId = -1)
    {
        var ourModule = await GetModuleForSystemByNameAsync(search, name, userId);

        if(ourModule == null)
            throw new NotFoundException($"Couldn't find module {name}!");
        
        return RefreshModule(service, ourModule);
    }

    /// <summary>
    /// Update the loaded module with the given ONLY IF it's newer, and always return whatever
    /// loaded module we can find regardless
    /// </summary>
    /// <param name="service"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public static LoadedModule RefreshModule(this IModuleService service, ContentView module)
    {
        //Specifically FALSE for forcing: we don't want to update modules that are the same as before here...
        return service.UpdateModule(module, false) ?? service.GetModule(module.name) ?? 
            throw new NotFoundException($"No loaded module found with name {module.name}!");
    }
}