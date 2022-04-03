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
            var modData = await modService.RefreshModuleAsync(services.searcher, name, user.id);
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
            var existing = await services.searcher.GetModuleForSystemByNameAsync(module.name, userId);

            if(existing != null)
                module.id = existing.id;
            else
                module.id = 0;
            
            //Need to add this just in case they don't, since this is a SPECIFIC module endpoint
            module.contentType = Db.InternalContentType.module;
            
            var result = await services.writer.WriteAsync(module, userId);
            modService.RefreshModule(result); //Make it ready immediately, just in case.
            return result;
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
            var modData = await modService.RefreshModuleAsync(services.searcher, name, requester);
            await Task.Run(() => result = modService.RunCommand(name, arguments, requester, parentId));
            return result;
        });
    }

    public class ModuleContentView : ContentView
    {
        public Dictionary<string, ModuleSubcommandInfo?> subcommands {get;set;} = new Dictionary<string, ModuleSubcommandInfo?>();
    }

    /// <summary>
    /// This additional endpoint is required in order to get all the parsed data from lua/etc
    /// </summary>
    /// <returns></returns>
    [HttpGet("search")]
    public Task<ActionResult<List<ModuleContentView>>> SearchModules([FromQuery]string? name = null, [FromQuery]string? fields = "*")
    {
        return MatchExceptions(async () =>
        {
            var userId = GetUserIdStrict();
            var values = new Dictionary<string, object> {
                { "type", Db.InternalContentType.module },
            };

            var query = "contentType = @type and !notdeleted()";

            if(name != null)
            {
                query += " and name like @name";
                values.Add("name", name);
            }

            var modules = await services.searcher.SearchSingleType<ModuleContentView>(userId, new SearchRequest()
            {
                type = "content",
                fields = fields ?? "*",
                query = query
            }, values);

            foreach(var m in modules)
            {
                var loaded = modService.RefreshModule(m);
                m.subcommands = loaded.subcommands;
            }

            return modules;
        });
    }
}