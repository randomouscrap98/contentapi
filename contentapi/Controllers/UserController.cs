using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using contentapi.Services;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using System.Security.Claims;
using AutoMapper;
using contentapi.Models;

namespace contentapi.Controllers
{
    public class UserSearch : EntitySearchBase
    {
        public string Username {get;set;}
    }

    public class UserSearchProfile : Profile
    {
        public UserSearchProfile()
        {
            CreateMap<UserSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Username));
        }
    }

    public class UserController : EntityBaseController<UserView>
    {
        protected IHashService hashService;
        protected ITokenService tokenService;
        protected ILanguageService languageService;
        protected IEmailService emailService;


        public const string UserIdentifier = "uid";

        public const string EmailKey = "se";
        public const string PasswordHashKey = "sph";
        public const string PasswordSaltKey = "sps";
        public const string RegistrationCodeKey = "srk";

        public UserController(ILogger<UserController> logger, IHashService hashService, IEntityProvider entityProvider,
            ITokenService tokenService, IMapper mapper, ILanguageService languageService,
            IEmailService emailService) : base(logger, entityProvider, mapper)
        { 
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
        }

        protected override UserView GetViewFromExpanded(EntityWrapper user)
        {
            var view = new UserView()
            {
                id = user.id,
                createDate = user.createDate,
                username = user.name,
            };

            return view;
        }

        protected UserView GetViewWithEmail(EntityWrapper user)
        {
            var view = GetViewFromExpanded(user);
            view.email = user.Values.First(x => x.key == EmailKey).value; //We're the creator so we get to see the email
            return view;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserView>>> GetAll([FromQuery]UserSearch search)
        {
            var entitySearch = LimitSearch(mapper.Map<EntitySearch>(search));
            return (await SearchAsync(entitySearch, true)).Select(x => GetViewFromExpanded(x)).ToList();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserView>> GetUser([FromRoute]long id)
        {
            var user = await FindByIdAsync(id, true);

            if(user == null)
                return NotFound($"User with id {id} not found");
            
            return GetViewFromExpanded(user);
        }


        [HttpGet("me")]
        public async Task<ActionResult<UserView>> Me()
        {
            //Look for the UID from the JWT 
            var id = User.FindFirstValue(UserIdentifier);

            if(id == null)
                return BadRequest("Not logged in!");

            return await GetUser(long.Parse(id));
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserCredential user)
        {
            EntityWrapper foundUser = null;

            if(user.username != null)
            {
                foundUser = await FindByNameAsync(user.username, true);
            }
            else if (user.email != null)
            {
                var foundEmail = await FindValueAsync(EmailKey, user.email);

                if(foundEmail != null)
                    foundUser = await FindByIdAsync(foundEmail.entityId, true);
            }

            //Should this be the same as bad password? eeeehhhh
            if(foundUser == null)
                return BadRequest("Must provide a valid username or email!");
            
            if(HasValue(foundUser, RegistrationCodeKey)) //There's a registration code pending
                return BadRequest("You must confirm your email first");

            var hash = hashService.GetHash(user.password, Convert.FromBase64String(GetValue(foundUser, PasswordSaltKey)));

            if(!hash.SequenceEqual(Convert.FromBase64String(GetValue(foundUser, PasswordHashKey))))
                return BadRequest("Password incorrect!");

            return tokenService.GetToken(new Dictionary<string, string>()
            {
                { UserIdentifier, foundUser.id.ToString() }
            });
        }


        //The rest is registration
        protected virtual async Task SendConfirmationEmailAsync(string recipient, string code)
        {
            var subject = languageService.GetString("ConfirmEmailSubject", "en");
            var body = languageService.GetString("ConfirmEmailBody", "en", new Dictionary<string, object>() {{"confirmCode", code}});
            await emailService.SendEmailAsync(new EmailMessage(recipient, subject, body));
        }

        //You 'Create' a new user by posting ONLY 'credentials'. This is different than most other types of things...
        //passwords and emails and such shouldn't be included in every view unlike regular models where every field is there.
        [HttpPost("register")]
        public async Task<ActionResult<UserView>> PostCredentials([FromBody]UserCredential user)
        {
            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                return BadRequest("Must provide a username!");
            if(user.email == null)
                return BadRequest("Must provide an email!");

            if(await FindByNameAsync(user.username) != null || await FindValueAsync(EmailKey, user.email) != null)
                return BadRequest("This user already seems to exist!");

            var salt = hashService.GetSalt();

            var newUser = QuickEntity(user.username);

            newUser.Values.AddRange(new[] {
                QuickValue(EmailKey, user.email),
                QuickValue(PasswordSaltKey, Convert.ToBase64String(salt)),
                QuickValue(PasswordHashKey, Convert.ToBase64String(hashService.GetHash(user.password, salt))),
                QuickValue(RegistrationCodeKey, Guid.NewGuid().ToString())
            });

            await entityProvider.WriteAsync(newUser);

            //Note the last parameter: the create user is ALWAYS the user that just got created! The user "creates" itself!
            //await LogAct(EntityAction.Create, createUser.entityId, createUser.entityId);

            return GetViewWithEmail(newUser);
        }

        [HttpPost("register/sendemail")]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]string email)
        {
            var emailValue = await FindValueAsync(EmailKey, email);

            if(emailValue == null)
                return BadRequest("No user with that email");

            //Now look up the registration code (that's all we need from user)
            var registrationCode = await FindValueAsync(RegistrationCodeKey, null, emailValue.entityId);

            if(registrationCode == null)
                return BadRequest("Nothing to do for user");

            await SendConfirmationEmailAsync(email, registrationCode.value);

            return Ok("Email sent");
        }

        [HttpPost("register/confirm")]
        public async Task<ActionResult> ConfirmEmail([FromBody]string confirmationKey)
        {
            if(string.IsNullOrEmpty(confirmationKey))
                return BadRequest("Must provide a confirmation key in the body");

            var confirmValue = await FindValueAsync(RegistrationCodeKey, confirmationKey);

            if(confirmValue == null)
                return BadRequest("No user found with confirmation key");

            await entityProvider.DeleteAsync(confirmValue);

            return Ok("Email Confirmed");
        }
    }
}