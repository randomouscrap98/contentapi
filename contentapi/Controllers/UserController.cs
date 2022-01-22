using contentapi.Main;
using contentapi.Security;
using contentapi.Views;
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

    public class UserCredentials
    {
        public string username {get;set;} = "";
        public string email {get;set;} = "";
        public string password {get;set;} = "";
    }

    public class UserLogin : UserCredentials
    {
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
        userService.InvalidateAllTokens(GetUserIdStrict());
    }

    [HttpPost("register")]
    public Task<ActionResult<UserView>> Register([FromBody]UserCredentials credentials)
    {
        //Like all controllers, we want ALL of the work possible to be inside a service, not the controller.
        //A service which can be tested!
        return MatchExceptions<UserView>(() =>
        {
            return userService.CreateNewUser(credentials.username, credentials.password, credentials.email);
        });
    }
}