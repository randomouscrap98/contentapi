using contentapi.Main;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class UserControllerConfig 
{
    public bool BackdoorRegistration {get;set;}
    public bool BackdoorEmailLog {get;set;}
}

public class UserController : BaseController
{
    protected IUserService userService;
    protected IGenericSearch searcher;
    protected UserControllerConfig config;
    protected IEmailService emailer;

    public UserController(BaseControllerServices services, IUserService userService,
        UserControllerConfig config, IGenericSearch searcher, IEmailService emailer)
        : base(services)
    {
        this.userService = userService;
        this.searcher = searcher;
        this.emailer = emailer;
        this.config = config;
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

    [HttpPost("sendregistrationcode")]
    public Task<ActionResult<bool>> SendRegistrationCode([FromBody]string email)
    {
        return MatchExceptions(async () =>
        {
            //Make sure the email exists or whatever
            var users = await searcher.GetByField<UserView>(RequestType.user, "email", email);

            if(users.Count != 1)
                throw new ArgumentException("Email not found!");
            
            var user = users.First();

            if(!user.registered)
                throw new ArgumentException("User associated with email already registered!");

            var registrationCode = await userService.GetRegistrationKeyAsync(user.id);

            //TODO: language? Configuration? I don't know
            await emailer.SendEmailAsync(new EmailMessage(email, "Registration instructions",
                $"Your registration code for '{Request.Host}' is:\n\n{registrationCode}"));

            return true;
        });
    }

    [HttpPost("confirmregistration/{id}")]
    public Task<ActionResult<string>> ConfirmRegistration([FromRoute]long id, [FromBody]string code)
    {
        return MatchExceptions(() =>
        {
            return userService.CompleteRegistration(id, code);
        });
    }

    [HttpGet("getregistrationcode/{id}")]
    public Task<ActionResult<string>> GetRegistrationCode([FromRoute]long id)
    {
        return MatchExceptions(async () =>
        {
            if(!config.BackdoorRegistration)
                throw new ForbiddenException("This is a debug endpoint that has been deactivated!");

            var registrationCode = await userService.GetRegistrationKeyAsync(id);

            return registrationCode;
        });
    }

    [HttpGet("emaillog")]
    public Task<ActionResult<List<EmailLog>>> GetEmailLog()
    {
        return MatchExceptions(() =>
        {
            if(!config.BackdoorEmailLog)
                throw new ForbiddenException("This is a debug endpoint that has been deactivated!");
            
            return Task.FromResult((emailer as NullEmailService ?? 
                throw new InvalidOperationException("The emailer is not set up for logging! This endpoint is only for the null email service")).Log);
        });
    }
}