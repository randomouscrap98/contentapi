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

        //[HttpGet] 
        //public ActionResult<List<string>> Get()
        //{
        //    return modules.Keys.ToList();
        //}

        //[HttpGet("{name}")]
        //public ActionResult<string> Get([FromRoute]string name)
        //{
        //    if(!modules.ContainsKey(name))
        //        return NotFound();
        //    
        //    return modules[name].code;
        //}

        //private void AddMessage(long uid, string message)
        //{
        //    moduleMessages.Add
        //}

        //[HttpPost("{name}")]
        //public ActionResult Post([FromRoute]string name, [FromBody]string code) //NOTE: CODE NOT ACTUALLY USED!!! JUST FOR DANG MUSTACHE!!
        //{
        //    if(!permService.IsSuper(GetRequesterNoFail()))
        //        return Unauthorized("Not allowed to post code for modules");

        //    if(!modules.ContainsKey(name))
        //        modules.Add(name, new TempModule());

        //    modules[name].code = code;

        //    var script = new Script();
        //    script.DoString(modules[name].code);          //This could take a LONG time.
        //    script.Globals["data"] = modules[name].saveData;
        //    script.Globals["sendmessage"] = new Action<long, string>((uid, message) =>
        //    {
        //        moduleMessages.Add(new ModuleMessage()
        //        {
        //            receiverUid = uid,
        //            module = name,
        //            message = message
        //        });
        //    });
        //    modules[name].script = script;

        //    return Ok();
        //}

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