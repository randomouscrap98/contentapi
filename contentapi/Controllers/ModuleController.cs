using contentapi.Main;
using contentapi.Module;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;


[Authorize()]
public class ModuleController : BaseController
{
    protected IModuleService modService;

    public ModuleController(BaseControllerServices services, IModuleService moduleService) : base(services) 
    { 
        this.modService = moduleService;
    }

    protected async Task<LoadedModule> RefreshModule(string name, long userId)
    {
        var modules = await services.searcher.SearchSingleType<ContentView>(userId, new SearchRequest()
        {
            type = "content",
            fields = "~votes,permissions,keywords,values",
            query = "name = @name and contentType = @type and !notdeleted()"
        }, new Dictionary<string, object> {
            { "name", name },
            { "type", Db.InternalContentType.module }
        });

        var ourModule = modules.FirstOrDefault(x => x.name == name && x.contentType == Db.InternalContentType.module); 

        if(ourModule == null)
            throw new NotFoundException($"Couldn't find module {name}!");
        
        //Specifically FALSE for forcing: we don't want to update modules that are the same as before here...
        return modService.UpdateModule(ourModule, false) ?? modService.GetModule(name) ?? 
            throw new NotFoundException($"No module found with name {name}!");
    }

    /// <summary>
    /// Modules can log debug information, useful for... well, debugging. Only supers can read these logs though!
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [HttpGet("debug/{name}")]
    public Task<ActionResult<List<string>>> GetDebug([FromRoute]string name)
    {
        return MatchExceptions(async () =>
        {
            RateLimit(RateModule);
            var user = await GetUserViewStrictAsync();
            if(!user.super)
                throw new ForbiddenException("Can't read debug information unless super!");
            var modData = await RefreshModule(name, user.id);
            return modData.debug.ToList();
        });
    }

    /// <summary>
    /// Allows you to POST either a new or updated module. The module service determines whether you have permission or not
    /// </summary>
    /// <param name="name"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    [HttpPost("byname")]
    public Task<ActionResult<ContentView>> PostByNameAsync([FromBody]ContentView module)
    {
        return MatchExceptions(async () =>
        {
            RateLimit(RateWrite);
            //Go find by name first
            var userId = GetUserIdStrict();
            var existing = await RefreshModule(module.name, userId); //services.searcher..FindByNameAsync(module.name);

            if(existing != null)
                module.id = existing.contentId;
            else
                module.id = 0;
            
            //Need to add this just in case they don't, since this is a SPECIFIC module endpoint
            module.contentType = Db.InternalContentType.module;
            
            return await services.writer.WriteAsync(module, userId);
        });
    }

    /// <summary>
    /// POST command data to a module. The arguments need not be parsed; just the full argument list as given (including subcommand)
    /// </summary>
    /// <param name="name">The module name</param>
    /// <param name="arguments">The rest of the command as a string</param>
    /// <param name="parentId">The room you're posting the module command in; MUST be provided!</param>
    /// <returns>NOT the module message produced by the commad, but essentially debug info</returns>
    [HttpPost("{name}/{parentId}")]
    public Task<ActionResult<string>> RunCommand([FromRoute]string name, [FromBody]string arguments, [FromRoute]long parentId)
    {
        return MatchExceptions(async () =>
        {
            RateLimit(RateModule);
            var requester = GetUserIdStrict();
            string result = "";
            //RunCommand should be thread safe, so just... run it async!
            await Task.Run(() => result = modService.RunCommand(name, arguments, requester, parentId));
            return result;
        });
    }

    public class ModuleContentView : ContentView
    {
        public Dictionary<string, ModuleSubcommandInfo?> subcommands {get;set;} = new Dictionary<string, ModuleSubcommandInfo?>();
    }

    [HttpGet("allmodules")]
    public Task<ActionResult<List<ModuleContentView>>> GetAllmodules()
    {
        return MatchExceptions(async () =>
        {
            var userId = GetUserIdStrict();
            var modules = await services.searcher.SearchSingleType<ModuleContentView>(userId, new SearchRequest()
            {
                type = "content",
                fields = "*",
                query = "contentType = @type and !notdeleted()"
            }, new Dictionary<string, object> {
                { "type", Db.InternalContentType.module }
            });

            foreach(var m in modules)
            {
                var loaded = await RefreshModule(m.name, userId);
                m.subcommands = loaded.subcommands;
            }

            return modules;
        });
    }
}