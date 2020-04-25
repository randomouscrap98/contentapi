using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using contentapi.Services;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using AutoMapper;
using contentapi.Services.Extensions;
using Randomous.EntitySystem.Extensions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace contentapi.Controllers
{
    public class UserSearch : EntitySearchBase
    {
        public string Username {get;set;}
    }

    public class UserControllerConfig
    {
        public int NameChangesPerTime {get;set;} //= 3;
        public TimeSpan NameChangeRange {get;set;}
    }

    public class UserControllerProfile : Profile
    {
        public UserControllerProfile()
        {
            CreateMap<UserSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Username));

            CreateMap<UserViewBasic, UserView>().ReverseMap();
            CreateMap<UserViewBasic, UserViewFull>().ReverseMap();
            CreateMap<UserView, UserViewFull>().ReverseMap();
            CreateMap<UserCredential, UserViewFull>().ReverseMap();
        }
    }

    public class UserController : BaseEntityController<UserViewFull>
    {
        protected IHashService hashService;
        protected ITokenService tokenService;
        protected ILanguageService languageService;
        protected IEmailService emailService;

        protected UserControllerConfig config;

        public UserController(ILogger<UserController> logger, ControllerServices services, IHashService hashService,
            ITokenService tokenService, ILanguageService languageService, IEmailService emailService,
            IOptionsMonitor<UserControllerConfig> config)
            :base(services, logger)
        { 
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
            this.config = config.CurrentValue;
        }

        protected override string EntityType => keys.UserType;

        protected override UserViewFull CreateBaseView(EntityPackage user)
        {
            var result = new UserViewFull() 
            { 
                username = user.Entity.name, 
                email = user.GetValue(keys.EmailKey).value, 
                super = services.systemConfig.SuperUsers.Contains(user.Entity.id),
                password = user.GetValue(keys.PasswordHashKey).value,
                salt = user.GetValue(keys.PasswordSaltKey).value
            };

            if(user.HasValue(keys.AvatarKey))
                result.avatar = long.Parse(user.GetValue(keys.AvatarKey).value);
            if(user.HasValue(keys.RegistrationCodeKey))
                result.registrationKey = user.GetValue(keys.RegistrationCodeKey).value;

            return result;
        }

        protected override EntityPackage CreateBasePackage(UserViewFull user)
        {
            var newUser = NewEntity(user.username)
                .Add(NewValue(keys.AvatarKey, user.avatar.ToString()))
                .Add(NewValue(keys.EmailKey, user.email))
                .Add(NewValue(keys.PasswordSaltKey, user.salt))
                .Add(NewValue(keys.PasswordHashKey, user.password));
            //Can't do anything about super
            
            if(!string.IsNullOrWhiteSpace(user.registrationKey))
                newUser.Add(NewValue(keys.RegistrationCodeKey, user.registrationKey));

            return newUser;
        }

        protected override async Task<UserViewFull> CleanViewGeneralAsync(UserViewFull view)
        {
            view = await base.CleanViewGeneralAsync(view);

            //Go look up the avatar. Make sure it's A FILE DAMGIT
            var file = await provider.FindByIdBaseAsync(view.avatar);

            if(file != null && (!file.type.StartsWith(keys.FileType) || !file.content.ToLower().StartsWith("image")))
                throw new BadRequestException("Avatar isn't an image type content!");

            return view;
        }

        protected async Task<List<EntityPackage>> GetAll(UserSearch search)
        {
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));
            return await provider.GetEntityPackagesAsync(entitySearch);
        }
        
        protected UserView GetView(EntityPackage package)
        {
            return services.mapper.Map<UserView>(ConvertToView(package));
        }

        protected async Task<EntityPackage> GetCurrentUser()
        {
            var id = GetRequesterUid();
            var user = await provider.FindByIdAsync(id);

            //A VERY SPECIFIC glitch you really only get in development 
            if (user == null)
                throw new UnauthorizedAccessException($"No user with uid {id}");
            
            return user;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserViewBasic>>> GetAsync([FromQuery]UserSearch search)
        {
            logger.LogDebug($"User GetAsync called by {GetRequesterUidNoFail()}");
            return (await GetAll(search)).Select(x => services.mapper.Map<UserViewBasic>(ConvertToView(x))).ToList();
        }

        [HttpGet("me")]
        [Authorize]
        public Task<ActionResult<UserView>> Me()
        {
            //The first is check, the second is return
            return ThrowToAction<UserView>(async () => 
            {
                return GetView(await GetCurrentUser());
            }); 
        }

        public class UserBasicPost
        {
            public long avatar {get;set;}
        }

        [HttpPut("basic")]
        [Authorize]
        public Task<ActionResult<UserView>> PutBasicAsync([FromBody]UserBasicPost data)
        {
            return ThrowToAction<UserView>(async () => 
            {
                var user = await GetCurrentUser();
                var userView = ConvertToView(user);

                //Only set avatar if they gave us something
                if(data.avatar >= 0)
                    userView.avatar = data.avatar;

                //if(!string.IsNullOrWhiteSpace(data.username))
                //    throw new BadRequestException("No username changes yet! Maybe soon!");

                return GetView(await WriteViewBaseAsync(userView));
            }); 
        }

        protected string GetToken(long id, TimeSpan? expireOverride = null)
        {
            return tokenService.GetToken(new Dictionary<string, string>()
            {
                { keys.UserIdentifier, id.ToString() }
            }, expireOverride);
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserAuthenticate user)
        {
            EntityPackage foundUser = null;

            if(user.username != null)
            {
                foundUser = await FindByNameAsync(user.username);
            }
            else if (user.email != null)
            {
                var foundEmail = await FindValueAsync(keys.EmailKey, user.email);

                if(foundEmail != null)
                    foundUser = await provider.FindByIdAsync(foundEmail.entityId);
            }

            //Should this be the same as bad password? eeeehhhh
            if(foundUser == null)
                return BadRequest("Must provide a valid username or email!");
            
            if(foundUser.HasValue(keys.RegistrationCodeKey)) //There's a registration code pending
                return BadRequest("You must confirm your email first");

            var userView = ConvertToView(foundUser);

            if(!Verify(userView, user.password))
                return BadRequest("Password incorrect!");

            TimeSpan? expireOverride = null;

            //Note: this allows users to create ultimate super long tokens for use like... forever. Until we get
            //the token expirer set up, this will be SCARY
            if(user.ExpireSeconds > 0)
                expireOverride = TimeSpan.FromSeconds(user.ExpireSeconds);

            return GetToken(foundUser.Entity.id, expireOverride);
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

            if(await FindByNameBaseAsync(user.username) != null || await FindValueAsync(keys.EmailKey, user.email) != null)
                return BadRequest("This user already seems to exist!");
            
            var fullUser = services.mapper.Map<UserViewFull>(user);

            SetPassword(fullUser, fullUser.password);
            fullUser.registrationKey = Guid.NewGuid().ToString();

            return await ThrowToAction(async() => GetView(await WriteViewBaseAsync(fullUser, (p) => 
            {
                //Before creating the user, we need to set the owner as themselves, not as anonymous.
                var creatorRelation = p.GetRelation(keys.CreatorRelation);
                creatorRelation.entityId1 = creatorRelation.entityId2;
                creatorRelation.value = creatorRelation.entityId2.ToString(); //Warn: this is VERY implementation specific! Kinda sucks to have two pieces of code floating around!
            })));
        }

        public class RegistrationEmailPost
        {
            public string email {get;set;}
        }

        [HttpPost("register/sendemail")]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]RegistrationEmailPost post)
        {
            var emailValue = await FindValueAsync(keys.EmailKey, post.email);

            if(emailValue == null)
                return BadRequest("No user with that email");

            //Now look up the registration code (that's all we need from user)
            var registrationCode = await FindValueAsync(keys.RegistrationCodeKey, null, emailValue.entityId);

            if(registrationCode == null)
                return BadRequest("Nothing to do for user");

            await SendConfirmationEmailAsync(post.email, registrationCode.value);

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

            var confirmValue = await FindValueAsync(keys.RegistrationCodeKey, post.confirmationKey);

            if(confirmValue == null)
                return BadRequest("No user found with confirmation key");

            var uid = confirmValue.entityId;
            await provider.DeleteAsync(confirmValue);

            return GetToken(uid);
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
            var user = await GetCurrentUser();
            var fullUser = ConvertToView(user);
            var output = new List<string>();

            if(!Verify(fullUser, change.oldPassword))
                return BadRequest("Old password incorrect!");

            if(!string.IsNullOrWhiteSpace(change.password))
            {
                SetPassword(fullUser, change.password);
                output.Add("Changed password");
            }

            if(!string.IsNullOrWhiteSpace(change.email))
            {
                fullUser.email = change.email;
                output.Add("Changed email");
            }

            if(!string.IsNullOrWhiteSpace(change.username))
            {
                if(change.username == fullUser.username)
                    return BadRequest("That's your current username!");

                var beginning = DateTime.Now - config.NameChangeRange;

                //Need historic users 
                var historicQuery = 
                    from r in Q<EntityRelation>()
                    join e in Q<Entity>() on r.entityId2 equals e.id
                    where r.entityId1 == user.Entity.id && EF.Functions.Like(r.type, keys.HistoryRelation) &&
                        r.createDate > beginning
                    select e;

                var historic = await provider.GetListAsync(historicQuery);
                var usernames = historic.Select(x => x.name).Append(fullUser.username).Append(change.username).Distinct();

                if(usernames.Count() > config.NameChangesPerTime)
                    return BadRequest($"Too many username changes in the given time: allowed {config.NameChangesPerTime} per {config.NameChangeRange}");
                
                fullUser.username = change.username;
                output.Add("Changed username");
            }

            await WriteViewAsync(fullUser);

            return Ok(string.Join(", ", output));
        }
    }
}