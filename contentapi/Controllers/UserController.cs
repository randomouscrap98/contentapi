using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using contentapi.Services;
using Microsoft.Extensions.Logging;
using AutoMapper;
using contentapi.Services.Implementations;
using contentapi.Services.Constants;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Controllers
{
    public class UserControllerConfig
    {
        public int NameChangesPerTime {get;set;} //= 3;
        public TimeSpan NameChangeRange {get;set;}
    }

    public class UserController : BaseSimpleController
    {
        protected IHashService hashService;
        protected ITokenService tokenService;
        protected ILanguageService languageService;
        protected IEmailService emailService;
        protected IMapper mapper;
        protected UserViewService service;

        protected UserControllerConfig config;

        public UserController(ILogger<UserController> logger, IHashService hashService,
            ITokenService tokenService, ILanguageService languageService, IEmailService emailService,
            UserControllerConfig config, UserViewService service, IMapper mapper)
            :base(logger)
        { 
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
            this.config = config;
            this.service = service;
            this.mapper = mapper;
        }

        protected async Task<UserViewFull> GetCurrentUser()
        {
            var requester = GetRequesterNoFail();
            var user = await service.FindByIdAsync(requester.userId, requester);

            //A VERY SPECIFIC glitch you really only get in development 
            if (user == null)
                throw new UnauthorizedAccessException($"No user with uid {requester.userId}");
            
            return user;
        }

        [HttpGet]
        public Task<ActionResult<IList<UserViewBasic>>> GetAsync([FromQuery]UserSearch search)
        {
            return ThrowToAction<IList<UserViewBasic>>(async () =>
            {
                return (await service.SearchAsync(search, GetRequesterNoFail())).Select(x => mapper.Map<UserViewBasic>(x)).ToList();
            });
        }

        [HttpGet("me")]
        [Authorize]
        public Task<ActionResult<UserView>> Me()
        {
            return ThrowToAction<UserView>(async () => 
            {
                return mapper.Map<UserView>(await GetCurrentUser());
            }); 
        }

        public class UserBasicPost
        {
            public long avatar {get;set;}

            [MaxLength(256)]
            public string special {get;set;} = null;

            public List<long> hidelist {get;set;} = null;
        }

        [HttpPut("basic")]
        [Authorize]
        public Task<ActionResult<UserView>> PutBasicAsync([FromBody]UserBasicPost data)
        {
            return ThrowToAction<UserView>(async () => 
            {
                var userView = await GetCurrentUser();

                //Only set avatar if they gave us something
                if(data.avatar >= 0)
                    userView.avatar = data.avatar;

                if(data.special != null)
                    userView.special = data.special;

                if(data.hidelist != null)
                    userView.hidelist = data.hidelist;

                return mapper.Map<UserView>(await service.WriteAsync(userView, GetRequesterNoFail()));
            }); 
        }

        protected string GetToken(long id, TimeSpan? expireOverride = null)
        {
            return tokenService.GetToken(new Dictionary<string, string>()
            {
                { Keys.UserIdentifier, id.ToString() }
            }, expireOverride);
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserAuthenticate user)
        {
            UserViewFull userView = null;
            var requester = GetRequesterNoFail();

            if(user.username != null)
                userView = await service.FindByUsernameAsync(user.username, requester);
            else if (user.email != null)
                userView = await service.FindByEmailAsync(user.email, requester);

            //Should this be the same as bad password? eeeehhhh
            if(userView == null)
                return BadRequest("Must provide a valid username or email!");
            
            if(!string.IsNullOrWhiteSpace(userView.registrationKey)) //There's a registration code pending
                return BadRequest("You must confirm your email first");

            if(!Verify(userView, user.password))
                return BadRequest("Password incorrect!");

            TimeSpan? expireOverride = null;

            //Note: this allows users to create ultimate super long tokens for use like... forever. Until we get
            //the token expirer set up, this will be SCARY
            if(user.ExpireSeconds > 0)
                expireOverride = TimeSpan.FromSeconds(user.ExpireSeconds);

            return GetToken(userView.id, expireOverride);
        }


        //The rest is registration
        protected virtual async Task SendConfirmationEmailAsync(string recipient, string code)
        {
            var subject = languageService.GetString("ConfirmEmailSubject", "en");
            var body = languageService.GetString("ConfirmEmailBody", "en", new Dictionary<string, object>() {{"confirmCode", code}});
            await emailService.SendEmailAsync(new EmailMessage(recipient, subject, body));
        }

        protected bool ValidUsername(string username)
        {
            if(Regex.IsMatch(username, @"[\s,|]"))
                return false;
            
            return true;
        }

        //You 'Create' a new user by posting ONLY 'credentials'. This is different than most other types of things...
        //passwords and emails and such shouldn't be included in every view unlike regular models where every field is there.
        [HttpPost("register")]
        public async Task<ActionResult<UserView>> Register([FromBody]UserCredential user)
        {
            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                return BadRequest("Must provide a username!");
            if(user.email == null)
                return BadRequest("Must provide an email!");
            if(string.IsNullOrWhiteSpace(user.password))
                return BadRequest("Must provide a password!");
            
            if(!ValidUsername(user.username))
                return BadRequest("Bad username: no spaces!");

            var requester = GetRequesterNoFail();

            if(await service.FindByUsernameAsync(user.username, requester) != null || await service.FindByEmailAsync(user.email, requester) != null)
                return BadRequest("This user already seems to exist!");
            
            var fullUser = mapper.Map<UserViewFull>(user);

            SetPassword(fullUser, fullUser.password);
            fullUser.registrationKey = Guid.NewGuid().ToString();

            return await ThrowToAction(async() => mapper.Map<UserView>(await service.WriteAsync(fullUser, requester)));
        }

        public class RegistrationEmailPost
        {
            public string email {get;set;}
        }

        [HttpPost("register/sendemail")]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]RegistrationEmailPost post)
        {
            var requester = GetRequesterNoFail();
            var foundUser = await service.FindByEmailAsync(post.email, requester);

            if(foundUser == null)
                return BadRequest("No user with that email");

            //Now look up the registration code (that's all we need from user)
            var registrationCode = foundUser.registrationKey;

            if(string.IsNullOrWhiteSpace(registrationCode))
                return BadRequest("Nothing to do for user");

            await SendConfirmationEmailAsync(post.email, foundUser.registrationKey); //registrationCode.value);

            return Ok("Email sent");
        }

        public class ConfirmEmailPost
        {
            public string confirmationKey {get;set;}
        }

        [HttpPost("register/confirm")]
        public async Task<ActionResult<string>> ConfirmEmail([FromBody]ConfirmEmailPost post)
        {
            if(string.IsNullOrEmpty(post.confirmationKey))
                return BadRequest("Must provide a confirmation key in the body");

            var requester = GetRequesterNoFail();

            var unconfirmedUser = await service.FindByRegistration(post.confirmationKey, requester);

            if(unconfirmedUser == null)
                return BadRequest("No user found with confirmation key");

            unconfirmedUser.registrationKey = null;

            //Clear out the registration. This is probably not good? These are implementation details, shouldn't be here
            var confirmedUser = await service.WriteAsync(unconfirmedUser, requester);

            return GetToken(confirmedUser.id);
        }

        protected bool Verify(UserViewFull user, string password)
        {
            //Get hash for given password using old hash to authenticate
            var hash = hashService.GetHash(password, Convert.FromBase64String(user.salt));
            return hash.SequenceEqual(Convert.FromBase64String(user.password));
        }

        protected void SetPassword(UserViewFull user, string newPassword)
        {
            var salt = hashService.GetSalt();
            user.salt = Convert.ToBase64String(salt);
            user.password = Convert.ToBase64String(hashService.GetHash(newPassword, salt));
        }

        [HttpPost("sensitive")]
        [Authorize]
        public async Task<ActionResult> SensitiveAsync([FromBody]SensitiveUserChange change)
        {
            var fullUser = await GetCurrentUser();
            var output = new List<string>();

            var requester = GetRequesterNoFail();

            if(!Verify(fullUser, change.oldPassword))
                return BadRequest("Old password incorrect!");

            if(!string.IsNullOrWhiteSpace(change.password))
            {
                SetPassword(fullUser, change.password);
                output.Add("Changed password");
            }

            if(!string.IsNullOrWhiteSpace(change.email))
            {
                if(await service.FindByEmailAsync(change.email, requester) != null)
                    return BadRequest("This email is already taken!");

                fullUser.email = change.email;
                output.Add("Changed email");
            }

            if(!string.IsNullOrWhiteSpace(change.username))
            {
                if(change.username == fullUser.username)
                    return BadRequest("That's your current username!");

                if(!ValidUsername(change.username))
                    return BadRequest("Bad username: no spaces!");

                //If two users come in at the same time and do this without locking, the world will crumble.
                if(await service.FindByUsernameAsync(change.username, requester) != null)
                    return BadRequest("Username already taken!");

                var beginning = DateTime.Now - config.NameChangeRange;

                //Need historic users 
                var historicUsers = await service.GetRevisions(fullUser.id, requester);
                var usernames = historicUsers.Select(x => x.username).Append(fullUser.username).Append(change.username).Distinct();

                if(usernames.Count() > config.NameChangesPerTime)
                    return BadRequest($"Too many username changes in the given time: allowed {config.NameChangesPerTime} per {config.NameChangeRange}");
                
                fullUser.username = change.username;
                output.Add("Changed username");
            }

            await service.WriteAsync(fullUser, requester);

            return Ok(string.Join(", ", output));
        }
    }
}