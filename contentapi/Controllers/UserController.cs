using contentapi.Main;
using contentapi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class UserController : BaseController
{
    protected IUserService userService;

    public UserController(BaseControllerServices services, IUserService userService)
        : base(services)
    {
        this.userService = userService;
    }

    public class UserLogin
    {
        public string username {get;set;} = "";
        public string email {get;set;} = "";
        public string password {get;set;} = "";
        public int expireSeconds {get;set;} = 0;
    }

    [HttpPost("login")]
    public async Task<ActionResult<string>> Login([FromBody]UserLogin loginInfo)
    {
        if(string.IsNullOrWhiteSpace(loginInfo.password))
            return BadRequest("Must provide password field!");
        if(string.IsNullOrWhiteSpace(loginInfo.username) && string.IsNullOrWhiteSpace(loginInfo.email))
           return BadRequest("Must provide either username or email!");

        TimeSpan? expireOverride = null;

        if(loginInfo.expireSeconds > 0)
            expireOverride = TimeSpan.FromSeconds(loginInfo.expireSeconds);
        
        return await MatchExceptions(() =>
        {
            if(!string.IsNullOrWhiteSpace(loginInfo.username))
                return userService.LoginUsernameAsync(loginInfo.username, loginInfo.password, expireOverride);
            else
                return userService.LoginEmailAsync(loginInfo.email, loginInfo.password, expireOverride);
        });
    }

    [Authorize]
    [HttpPost("invalidateall")]
    public void InvalidateAll()
    {
        userService.InvalidateAllTokens(GetUserId() ?? throw new InvalidOperationException("SOMEHOW YOU WEREN'T LOGGED IN???"));
    }
}