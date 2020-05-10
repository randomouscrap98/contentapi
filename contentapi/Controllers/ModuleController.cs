using System.Collections.Generic;
using System.Linq;
using contentapi.Services;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;

namespace contentapi.Controllers
{
    [Authorize]
    public class ModuleController : BaseSimpleController
    {
        //Just for now, testing, etc.
        public class TempModule
        {
            public string code = null;
            public Script script = null;
            public Dictionary<string, string> saveData = new Dictionary<string, string>();
        }

        public static Dictionary<string, TempModule> modules = new Dictionary<string, TempModule>();

        protected UserViewService service;

        public ModuleController(ILogger<BaseSimpleController> logger, UserViewService service) 
            : base(logger) 
        { 
            this.service = service;
        }

        [HttpGet] 
        public ActionResult<List<string>> Get()
        {
            return modules.Keys.ToList();
        }

        [HttpGet("{name}")]
        public ActionResult<string> Get([FromRoute]string name)
        {
            if(!modules.ContainsKey(name))
                return NotFound();
            
            return modules[name].code;
        }

        [HttpPost("{name}")]
        public ActionResult Post([FromRoute]string name, [FromBody]string code)
        {
            if(!modules.ContainsKey(name))
                modules.Add(name, new TempModule());
            
            modules[name].code = code;

            var script = new Script();
            script.DoString(code);          //This could take a LONG time.
            //script.Globals["GetUser"] = (Func<int, Table>)
            modules[name].script = script;

            return Ok();
        }

        //[HttpGet("{name}/{command}")]
        //public ActionResult<string> RunCommand([FromRoute]string name, [FromRoute]string command, [FromQuery]string data)
        //{

        //}
    }
}