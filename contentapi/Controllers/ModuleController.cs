using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;

namespace contentapi.Controllers
{
    public class ModuleController : BaseViewServiceController<ModuleViewService, ModuleView, ModuleSearch>
    {

        public ModuleController(ILogger<ModuleController> logger, ModuleViewService service)//UserViewService service, IPermissionService permissionService) 
            : base(logger, service) 
        { 
            //this.service = service;
            //this.permService = permissionService;
        }

        [HttpPost("{name}")]
        public Task<ActionResult<ModuleView>> PostByNameAsync([FromRoute]string name, [FromBody]ModuleView module)
        {
            return ThrowToAction(async () =>
            {
                //Go find by name first
                var existing = await service.FindByNameAsync(name);

                if(existing != null)
                    module.id = existing.Entity.id;
                else
                    module.id = 0;
                
                return await service.WriteAsync(module, GetRequesterNoFail());
            });
        }

        //This is a TEST endpoint!!!
        //[HttpGet("allmessages")]
        //public ActionResult<List<ModuleMessage>> AllMessages()
        //{
        //    return moduleMessages;
        //}

        //[HttpGet("{name}/{command}")]
        //public ActionResult RunCommand([FromRoute]string name, [FromRoute]string command, [FromQuery]string data)
        //{
        //    if(!modules.ContainsKey(name))
        //        return BadRequest($"No module with name {name}");
        //    
        //    var mod = modules[name];
        //    var cmdfuncname = $"command_{command}";
        //    var requester = GetRequesterNoFail();

        //    DynValue res = mod.script.Call(mod.script.Globals[cmdfuncname], requester.userId, data);
        //    
        //    //try
        //    //{
        //    //}
        //    //catch(Exception ex)
        //    //{
        //    //    return BadRequest($"Command failed: {ex.Message}");
        //    //}

        //    return Ok(res.String);
        //}
    }
}