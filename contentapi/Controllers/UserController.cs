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

    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        protected ILogger<UserController> logger;
        protected IHashService hashService;
        protected IEntityProvider entityProvider;
        protected IEmailService emailService;
        protected ILanguageService languageService;
        protected ITokenService tokenService;
        protected IMapper mapper;

        public const string UserIdentifier = "uid";

        public const string EmailKey = "se";
        public const string PasswordHashKey = "sph";
        public const string PasswordSaltKey = "sps";
        public const string RegistrationCodeKey = "srk";

        public UserController(ILogger<UserController> logger, IHashService hashService, IEntityProvider entityProvider,
            IEmailService emailService, ILanguageService languageService, ITokenService tokenService, IMapper mapper) 
        { 
            this.logger = logger;
            this.hashService = hashService;
            this.entityProvider = entityProvider;
            this.emailService = emailService;
            this.languageService = languageService;
            this.tokenService = tokenService;
            this.mapper = mapper;
        }

        protected async Task<List<Entity>> FindByUsernameAsync(string username)
        {
            return await entityProvider.GetEntitiesAsync(new EntitySearch() { NameLike = username });
        }

        protected async Task<List<EntityValue>> FindByEmailAsync(string email)
        {
            return await entityProvider.GetEntityValuesAsync(new EntityValueSearch() { KeyLike = EmailKey, ValueLike = email});
        }

        protected async Task<List<Entity>> FindByIdAsync(long id)
        {
            var search = new EntitySearch();
            search.Ids.Add(id);
            return await entityProvider.GetEntitiesAsync(search);
        }

        protected async Task<List<EntityValue>> FindRegistrationCodeAsync(long id)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = RegistrationCodeKey };
            valueSearch.EntityIds.Add(id);
            return await entityProvider.GetEntityValuesAsync(valueSearch);
        }

        protected UserView GetViewFromExpanded(EntityPackage user, bool includeEmail = false)
        {
            var view = new UserView()
            {
                id = user.Entity.id,
                createDate = user.Entity.createDate,
                username = user.Entity.name,
            };

            if(includeEmail)
                view.email = user.Values[EmailKey].First().value; //We're the creator so we get to see the email
            
            return view;
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

            var existingByUsername = await FindByUsernameAsync(user.username);
            var existingByEmail = await FindByEmailAsync(user.email);

            if(existingByUsername.Count() > 0 || existingByEmail.Count() > 0)
                return BadRequest("This user already seems to exist!");

            var salt = hashService.GetSalt();

            var newUser = new EntityPackage()
            {
                Entity = new Entity()
                {
                    name = user.username,
                    createDate = DateTime.Now
                }
            };

            entityProvider.AddValues(newUser, 
                new EntityValue() {
                    key = EmailKey,
                    value = user.email
                },
                new EntityValue() {
                    key = PasswordSaltKey,
                    value = Convert.ToBase64String(salt)
                },
                new EntityValue() {
                    key = PasswordHashKey,
                    value = Convert.ToBase64String(hashService.GetHash(user.password, salt))
                },
                new EntityValue() {
                    key = RegistrationCodeKey,
                    value = Guid.NewGuid().ToString()
                });

            await entityProvider.WriteAsync(newUser);

            //Note the last parameter: the create user is ALWAYS the user that just got created! The user "creates" itself!
            //await LogAct(EntityAction.Create, createUser.entityId, createUser.entityId);

            return GetViewFromExpanded(newUser, true);
        }

        [HttpGet]
        public async Task<ActionResult<List<UserView>>> GetUsers([FromQuery]UserSearch search)
        {
            var entitySearch = mapper.Map<EntitySearch>(search);
            if(entitySearch.Limit < 0 || entitySearch.Limit > 1000)
                entitySearch.Limit = 1000;
            var entities = await entityProvider.GetEntitiesAsync(entitySearch);
            var realEntities = await entityProvider.ExpandAsync(entities.ToArray());
            return realEntities.Select(x => GetViewFromExpanded(x)).ToList();
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<UserView>> GetUser([FromRoute]long id)
        {
            var user = await FindByIdAsync(id);

            if(user.Count != 1)
                return NotFound($"User with id {id} not found");
            
            var fullUser = (await entityProvider.ExpandAsync(user.First())).First(); //Allow this to throw exceptions (for now)
            return GetViewFromExpanded(fullUser);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserView>> Me()
        {
            //var uid = services.session.GetCurrentUid();
            var id = User.FindFirstValue(UserIdentifier);

            if(id == null)
                return BadRequest("Not logged in!");

            return await GetUser(long.Parse(id)); //GetSingle(services.session.GetCurrentUid());
        }

        //Temp class stuff to make life... easier?
        public class SendEmailBody
        {
            public string email {get;set;}
        }

        protected virtual async Task SendConfirmationEmailAsync(string recipient, string code)
        {
            var subject = languageService.GetString("ConfirmEmailSubject", "en");
            var body = languageService.GetString("ConfirmEmailBody", "en", new Dictionary<string, object>() {{"confirmCode", code}});
            await emailService.SendEmailAsync(new EmailMessage(recipient, subject, body));
        }

        [HttpPost("register/sendemail")]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]SendEmailBody data)
        {
            var foundValue = await FindByEmailAsync(data.email);

            if(foundValue.Count() == 0)
                return BadRequest("No user with that email");

            //Now look up the registration code (that's all we need from user)
            var registrationCode = await FindRegistrationCodeAsync(foundValue.First().entityId);

            if(registrationCode.Count() == 0)
                return BadRequest("Nothing to do for user");

            await SendConfirmationEmailAsync(data.email, registrationCode.First().value);

            return Ok("Email sent");
        }

        //Temp class stuff to make life... easier?
        public class ConfirmBody
        {
            public string confirmationKey {get;set;}
        }

        [HttpPost("register/confirm")]
        public async Task<ActionResult> ConfirmEmail([FromBody]ConfirmBody data)
        {
            if(string.IsNullOrEmpty(data.confirmationKey))
                return BadRequest("Must provide a confirmation key in the body");

            var valueSearch = new EntityValueSearch() { KeyLike = RegistrationCodeKey, ValueLike = data.confirmationKey };
            var values = await entityProvider.GetEntityValuesAsync(valueSearch);

            if(values.Count() == 0)
                return BadRequest("No user found with confirmation key");

            await entityProvider.DeleteAsync(values.ToArray());

            return Ok("Email Confirmed");
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserCredential user)
        {
            Entity foundUser = null;

            if(user.username != null)
            {
                foundUser = (await FindByUsernameAsync(user.username)).FirstOrDefault();
            }
            else if (user.email != null)
            {
                var foundEmail = (await FindByEmailAsync(user.email)).FirstOrDefault();
                if(foundEmail != null)
                    foundUser = (await FindByIdAsync(foundEmail.entityId)).FirstOrDefault(); 
            }

            //Should this be the same as bad password? eeeehhhh
            if(foundUser == null)
                return BadRequest("Must provide a valid username or email!");
            
            var completeUser = (await entityProvider.ExpandAsync(foundUser)).First();

            if(completeUser.Values.ContainsKey(RegistrationCodeKey)) //There's a registration code pending
                return BadRequest("You must confirm your email first");

            var hash = hashService.GetHash(user.password, Convert.FromBase64String(completeUser.Values[PasswordSaltKey].First().value));

            if(!hash.SequenceEqual(Convert.FromBase64String(completeUser.Values[PasswordHashKey].First().value)))
                return BadRequest("Password incorrect!");

            return tokenService.GetToken(new Dictionary<string, string>()
            {
                { "uid", completeUser.Entity.id.ToString() }
            });
        }
    }
}