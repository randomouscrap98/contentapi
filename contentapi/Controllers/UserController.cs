using contentapi.Security;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class UserController : BaseController
{
    protected IHashService hashService;

    public UserController(BaseControllerServices services, IHashService hashService)
        : base(services)
    {
        this.hashService = hashService;
    }

    public class UserLogin
    {
        public string username {get;set;} = "";
        public string email {get;set;} = "";
        public string password {get;set;} = "";
    }

    [HttpPost("login")]
    public ActionResult<string> Login([FromBody]UserLogin loginInfo)
    {
        if(string.IsNullOrWhiteSpace(loginInfo.password))
            return BadRequest("Must provide password field!");
        if(string.IsNullOrWhiteSpace(loginInfo.username) && string.IsNullOrWhiteSpace(loginInfo.email))
           return BadRequest("Must provide either username or email!");
        
        throw new NotImplementedException("No login yet");
    }
}