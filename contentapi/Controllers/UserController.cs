using contentapi.Main;
using contentapi.Utilities;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;

namespace contentapi.Controllers;

public class UserControllerConfig 
{
    public bool BackdoorRegistration {get;set;} = false;
    public bool BackdoorSuper {get;set;} = false; //THIS IS SO IMPORTANT TO BE FALSE AAAA
    public bool AccountCreationEnabled {get;set;} = true;
    public string ConfirmationType {get;set;} = "Standard";
    //Also accepts "Instant" and "Restricted:email,email,etc"
    public string HostName {get;set;} = "";
}

public class UserController : BaseController
{
    protected IUserService userService;
    protected UserControllerConfig config;
    protected IEmailService emailer;
    protected IRandomGenerator random;

    protected static RegistrationConfiguration? RegistrationOverride = null;

    const string InstantConfirmation = "Instant";
    const string StandardConfirmation = "Standard";
    const string RestrictedConfirmation = "Restricted:";

    public UserController(BaseControllerServices services, IUserService userService,
        UserControllerConfig config, IEmailService emailer, IRandomGenerator random)
        : base(services)
    {
        this.userService = userService;
        this.emailer = emailer;
        this.config = config;
        this.random = random;
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

    public class ConfirmRegistrationData 
    {
        public string email {get;set;} = "";
        public string key {get;set;} = "";
    }

    protected bool IsAccountCreationEnabled()
    {
        if(RegistrationOverride != null)
            return RegistrationOverride.enabled;
        else
            return config.AccountCreationEnabled;
    }

    public class RegistrationConfiguration
    {
        public bool enabled {get;set;}    
    }

    [HttpGet("registrationconfig")]
    public ActionResult<RegistrationConfiguration> GetAccountCreationEnabled()
    {
        return new RegistrationConfiguration {
            enabled = IsAccountCreationEnabled()
        };
    }

    [Authorize]
    [HttpPost("registrationconfig")]
    public Task<ActionResult<RegistrationConfiguration>> SetAccountCreationEnabled([FromBody]RegistrationConfiguration config)
    {
        return MatchExceptions(async () =>
        {
            var user = await GetUserViewStrictAsync();
            if(!user.super)
                throw new ForbiddenException("Only supers can set account creation status!");
            RegistrationOverride = config;
            return RegistrationOverride;
        });
    }

    [HttpPost("login")]
    public Task<ActionResult<string>> Login([FromBody]UserLogin loginInfo)
    {
        return MatchExceptions(() =>
        {
            if(string.IsNullOrWhiteSpace(loginInfo.password))
                throw new RequestException("Must provide password field!");
            if(string.IsNullOrWhiteSpace(loginInfo.username) && string.IsNullOrWhiteSpace(loginInfo.email))
                throw new RequestException("Must provide either username or email!");

            TimeSpan? expireOverride = null;

            if(loginInfo.expireSeconds > 0)
                expireOverride = TimeSpan.FromSeconds(loginInfo.expireSeconds);
        
            if(!string.IsNullOrWhiteSpace(loginInfo.username))
            {
                RateLimit(RateLogin, loginInfo.username);
                return userService.LoginUsernameAsync(loginInfo.username, loginInfo.password, expireOverride);
            }
            else
            {
                RateLimit(RateLogin, loginInfo.email);
                return userService.LoginEmailAsync(loginInfo.email, loginInfo.password, expireOverride);
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public Task<ActionResult<UserView>> GetMe()
    {
        return MatchExceptions(() =>
        {
            var uid = GetUserIdStrict();
            return CachedSearcher.GetById<UserView>(RequestType.user, uid, true);
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
        return MatchExceptions<UserView>(async () =>
        {
            if(!IsAccountCreationEnabled())
                throw new ForbiddenException("We're sorry, account creation is disabled at this time");

            var userId = await userService.CreateNewUser(credentials.username, credentials.password, credentials.email);
            var result = await CachedSearcher.GetById<UserView>(RequestType.user, userId);

            if(config.ConfirmationType == InstantConfirmation)
            {
                services.logger.LogDebug("Instant user account creation set, completing registration immediately");
                var token = await userService.CompleteRegistration(userId, await userService.GetRegistrationKeyAsync(userId));
                result.special = token;
            }

            return result;
        });
    }

    protected List<string> GetRestrictedEmails()
    {
        return config.ConfirmationType.Substring(RestrictedConfirmation.Length).Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
    }

    protected string GetHost()
    {
        if(!string.IsNullOrWhiteSpace(config.HostName))
            return config.HostName;
        else
            return Request.Host.ToString();
    }

    [HttpPost("sendregistrationcode")]
    public Task<ActionResult<bool>> SendRegistrationCode([FromBody]string email)
    {
        return MatchExceptions(async () =>
        {
            if(config.ConfirmationType == InstantConfirmation)
                throw new RequestException("User account creation is instant, there is no registration code");
            
            var userId = await userService.GetUserIdFromEmailAsync(email);
            var registrationCode = await userService.GetRegistrationKeyAsync(userId);
            var user = await CachedSearcher.GetById<UserView>(RequestType.user, userId);

            if(string.IsNullOrWhiteSpace(registrationCode))
                throw new RequestException("Couldn't find registration code for this email! Probably already registered!");

            if(config.ConfirmationType.StartsWith(RestrictedConfirmation))
            {
                var message = new EmailMessage();
                message.Recipients = GetRestrictedEmails();
                message.Title = $"User {user.username} would like to create an account";
                message.Body = $"User {user.username} is trying to create an account using email {email} on {GetHost()}\n\nIf this looks acceptable, please send them " +
                    $"an email with instructions on how to create an account, using registration code:\n\n{registrationCode}";

                //TODO: language? Configuration? I don't know
                await emailer.SendEmailAsync(message);
            }
            else
            {
                await emailer.SendEmailAsync(new EmailMessage(email, "Registration instructions",
                    $"Your registration code for '{GetHost()}' is:\n\n{registrationCode}"));
            }

            return true;
        });
    }

    [HttpPost("confirmregistration")]
    public Task<ActionResult<string>> ConfirmRegistration([FromBody]ConfirmRegistrationData confirmation)
    {
        return MatchExceptions(async () =>
        {
            if(config.ConfirmationType == InstantConfirmation)
                throw new RequestException("User account creation is instant, there is no registration code");

            var userId = await userService.GetUserIdFromEmailAsync(confirmation.email);
            return await userService.CompleteRegistration(userId, confirmation.key);
        });
    }

    [HttpPost("sendpasswordrecovery")]
    public Task<ActionResult<bool>> SendPasswordRecovery([FromBody]string email)
    {
        return MatchExceptions(async () =>
        {
            if(config.ConfirmationType == InstantConfirmation)
                throw new RequestException("User account creation is instant, meaning there is no email system in place and no way to recover passwords!");
            
            var userId = await userService.GetUserIdFromEmailAsync(email);

            var tempPassword = userService.GetTemporaryPassword(userId);
            var utcExpire = tempPassword.ExpireDate.ToUniversalTime();

            //TODO: language? Configuration? I don't know
            await emailer.SendEmailAsync(new EmailMessage(email, "Account Recovery",
                $"You can temporarily access your account on '{GetHost()}' for another {StaticUtils.HumanTime(utcExpire - DateTime.UtcNow)} using the ONE TIME USE temporary password:\n\n{tempPassword.Key}"));

            return true;
        });
    }

    [Authorize()]
    [HttpGet("privatedata")]
    public Task<ActionResult<UserGetPrivateData>> GetPrivateData()
    {
        return MatchExceptions(() => userService.GetPrivateData(GetUserIdStrict()));
    }

    public class UserSetPrivateDataProtected : UserSetPrivateData
    {
        public string currentEmail {get;set;} = "";
        public string currentPassword {get;set;} = "";
    }

    //NOTE: This used to be an "authorize" endpoint, but then the catch-22 of needing a password to CHANGE your password
    //came up, so now it takes a full login
    [HttpPost("privatedata")]
    public Task<ActionResult<string>> SetPrivateData([FromBody]UserSetPrivateDataProtected data)
    {
        return MatchExceptions(async () => 
        {
            var userId = await userService.GetUserIdFromEmailAsync(data.currentEmail);
            var token = await userService.LoginEmailAsync(data.currentEmail, data.currentPassword);
            await userService.SetPrivateData(userId, data);
            return token;
        });
    }

    private async Task<string> GetRegistrationCode(long id)
    {
        if(!config.BackdoorRegistration)
            throw new ForbiddenException("This is a debug endpoint that has been deactivated!");

        var registrationCode = await userService.GetRegistrationKeyAsync(id);

        return registrationCode;
    }

    [HttpGet("debug/getregistrationcode/{id}")]
    public Task<ActionResult<string>> GetRegistrationCodeById([FromRoute]long id)
    {
        return MatchExceptions(() => GetRegistrationCode(id));
    }

    [HttpGet("debug/getregistrationcodebyusername/{username}")]
    public Task<ActionResult<string>> GetRegistrationCodeByUsername([FromRoute]string username)
    {
        return MatchExceptions(async () =>
        {
            var userId = await userService.GetUserIdFromUsernameAsync(username);
            return await GetRegistrationCode(userId);
        });
    }

    //A debug endpoint to set the given uid to super. It only sets the super status to true, you can't set it to
    //false through this. Also, please always ensure this endpoint is disabled for anything you deploy! It is 
    //disabled by default, just be careful!
    [HttpPost("debug/setsuper/{uid}")]
    public Task<ActionResult<bool>> SetSuperDebug([FromRoute]long uid)
    {
        return MatchExceptions(async () =>
        {
            if(!config.BackdoorSuper)
                throw new ForbiddenException("This is a debug endpoint that has been deactivated!");
            
            await userService.SetSuperStatus(uid, true);
            
            return true;
        });
    }
}