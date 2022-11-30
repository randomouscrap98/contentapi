using contentapi.Main;
using contentapi.Utilities;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;

namespace contentapi.Controllers;

public class UserControllerConfig 
{
    public bool BackdoorRegistration {get;set;}
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
            if(!config.AccountCreationEnabled)
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
                //TODO: language? Configuration? I don't know
                //if(!string.IsNullOrWhiteSpace(config.CustomEmailBody))
                //{
                //    var emailMessage = new EmailMessage (email, "Registration instructions", config.CustomEmailBody.Replace("{{code}}", registrationCode));
                //    emailMessage.IsHtml = true;
                //    await emailer.SendEmailAsync(emailMessage);
                //}
                //else
                //{
                await emailer.SendEmailAsync(new EmailMessage(email, "Registration instructions",
                    $"Your registration code for '{GetHost()}' is:\n\n{registrationCode}"));
                //}
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

    [HttpPost("sendpasswordrecovery")]
    public Task<ActionResult<bool>> SendPasswordRecovery([FromBody]string email)
    {
        return MatchExceptions(async () =>
        {
            if(config.ConfirmationType == InstantConfirmation)
                throw new RequestException("User account creation is instant, meaning there is no email system in place and no way to recover passwords!");
            
            var userId = await userService.GetUserIdFromEmailAsync(email);

            //if(await userService.IsPasswordExpired(userId))
            //{
            //    //Generate a new password for the user. Is this OK? They MUST have a new password
            //    //in order to change values for themselves, LIKE their password itself! They're stuck
            //    //in a contradiction if not: their password is expired, but the only way to change
            //    //your password is to HAVE a valid password...
            //    var secretData = new UserSetPrivateData() {
            //        password = random.GetRandomPassword()
            //    };

            //    await userService.SetPrivateData(userId, secretData);

            //    await emailer.SendEmailAsync(new EmailMessage(email, "Account Recovery",
            //        $"Someone (hopefully you!) requested account recovery on '{GetHost()}' for the user associated with this email. Usually this generates " +
            //        "a temporary one-time-use password which expires quickly. However, because your password is expired, we have generated a new " +
            //        "permanent password for you. We recommend you change it as soon as possible"));
            //}
            //else
            //{
                var tempPassword = userService.GetTemporaryPassword(userId);
                var utcExpire = tempPassword.ExpireDate.ToUniversalTime();

                //TODO: language? Configuration? I don't know
                await emailer.SendEmailAsync(new EmailMessage(email, "Account Recovery",
                    $"You can temporarily access your account on '{GetHost()}' for another {StaticUtils.HumanTime(utcExpire - DateTime.UtcNow)} using the ONE TIME USE temporary password:\n\n{tempPassword.Key}"));
            //}
            //var user = await CachedSearcher.GetById<UserView>(RequestType.user, userId);

            //if(config.ConfirmationType.StartsWith(RestrictedConfirmation))
            //{
            //    var message = new EmailMessage();
            //    message.Recipients = GetRestrictedEmails();
            //    message.Title = $"User {user.username} is trying to recover their account";
            //    message.Body = $"User {user.username} is trying to recover their account using email {email} on {GetHost()}\n\nIf this looks acceptable, please send them " +
            //        $"an email stating they have a ONE TIME USE temporary password that will last until {utcExpire} UTC ({StaticUtils.HumanTime(utcExpire - DateTime.UtcNow)}):\n\n{tempPassword.Key}";

            //    //TODO: language? Configuration? I don't know
            //    await emailer.SendEmailAsync(message);
            //}
            //else
            //{

            //}

            return true;
        });
    }

    //[HttpGet("emaillog")]
    //public Task<ActionResult<List<EmailLog>>> GetEmailLog()
    //{
    //    return MatchExceptions(() =>
    //    {
    //        if(!config.BackdoorEmailLog)
    //            throw new ForbiddenException("This is a debug endpoint that has been deactivated!");
    //        
    //        return Task.FromResult((emailer as NullEmailService ?? 
    //            throw new InvalidOperationException("The emailer is not set up for logging! This endpoint is only for the null email service")).Log);
    //    });
    //}

    [Authorize()]
    [HttpGet("privatedata")]
    public Task<ActionResult<UserGetPrivateData>> GetPrivateData()
    {
        return MatchExceptions(() => userService.GetPrivateData(GetUserIdStrict()));
    }

    public class UserSetPrivateDataProtected : UserSetPrivateData
    {
        public string currentPassword {get;set;} = "";
    }

    [Authorize()]
    [HttpPost("privatedata")]
    public Task<ActionResult<bool>> SetPrivateData([FromBody]UserSetPrivateDataProtected data)
    {
        return MatchExceptions(async () => 
        {
            var userId = GetUserIdStrict();
            await userService.VerifyPasswordAsync(userId, data.currentPassword); //have to make sure the password given is accurate
            await userService.SetPrivateData(userId, data);
            return true;
        });
    }
}