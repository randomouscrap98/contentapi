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
using contentapi.Services.Extensions;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public class UserSearch : EntitySearchBase
    {
        public string Username {get;set;}
    }

    public class UserControllerProfile : Profile
    {
        public UserControllerProfile()
        {
            CreateMap<UserSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Username));
            CreateMap<UserView, UserViewBasic>().ReverseMap();
            CreateMap<UserView, UserViewFull>().ReverseMap();
            CreateMap<UserCredential, UserViewFull>().ReverseMap();
        }
    }

    public class UserController : EntityBaseController<UserView> //: EntityBaseController<UserView>
    {
        protected IHashService hashService;
        protected ITokenService tokenService;
        protected ILanguageService languageService;
        protected IEmailService emailService;

        public UserController(ILogger<UserController> logger, ControllerServices services, IHashService hashService,
            ITokenService tokenService, ILanguageService languageService, IEmailService emailService)
            :base(services, logger)
        { 
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
        }

        protected override string EntityType => keys.UserType;

        protected override UserView CreateBaseView(EntityPackage user)
        {
            return new UserView() 
            { 
                username = user.Entity.name, 
                email = user.GetValue(keys.EmailKey).value, 
                super = services.systemConfig.SuperUsers.Contains(user.GetRelation(keys.StandInRelation).entityId1)
            };
        }

        protected override EntityPackage CreateBasePackage(UserView view)
        {
            var user = (UserViewFull)view;
            var salt = hashService.GetSalt();

            //This is incorrect: we are not necessarily making a NEW user, we are setting up a package 
            //to look like the given view. This should work this way but be careful and fix later.
            var newUser = NewEntity(user.username)
                .Add(NewValue(keys.EmailKey, user.email))
                .Add(NewValue(keys.PasswordSaltKey, user.salt)) //Convert.ToBase64String(user.salt)))
                .Add(NewValue(keys.PasswordHashKey, user.password)) //Convert.ToBase64String(hashService.GetHash(user.password, salt))))
                .Add(NewValue(keys.RegistrationCodeKey, user.registrationKey)); //Guid.NewGuid().ToString()));

            return newUser;
        }

        //protected UserView GetViewWithEmail(EntityPackage user)
        //{
        //    var view = ConvertToView(user);
        //    view.email = user.Values.First(x => x.key == services.keys.EmailKey).value; //We're the creator so we get to see the email
        //    return view;
        //}

        protected async Task<List<EntityPackage>> GetAll(UserSearch search)
        {
            var entitySearch = (EntitySearch)(await ModifySearchAsync(services.mapper.Map<EntitySearch>(search)));
            return await services.provider.GetEntityPackagesAsync(entitySearch);//.ToList();
        }

        [HttpGet]
        public async Task<ActionResult<List<UserViewBasic>>> Get([FromQuery]UserSearch search)
        {
            return (await GetAll(search)).Select(x => services.mapper.Map<UserViewBasic>(ConvertToView(x))).ToList();
            //{
            //    var view = ConvertToView(x);
            //    view.email = null; //disable emails for general gets (even if it's yours)
            //    return view;
            //}).ToList();
        }

        //[HttpGet("{id}")]
        //public async Task<ActionResult<UserView>> GetSingle([FromRoute]long id)
        //{
        //    var user = await services.provider.FindByIdAsync(id);

        //    if(user == null)
        //        return NotFound($"User with id {id} not found");
        //    
        //    return GetViewFromExpanded(user);
        //}


        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserView>> Me()
        {
            long id = 0;
            
            try { id = GetRequesterUid(); }
            catch(Exception ex) { return BadRequest(ex.Message); }

            return ConvertToView(await FindByIdAsync(id));
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserCredential user)
        {
            EntityPackage foundUser = null;

            if(user.username != null)
            {
                foundUser = await services.provider.FindByNameAsync(user.username);
            }
            else if (user.email != null)
            {
                var foundEmail = await services.provider.FindValueAsync(keys.EmailKey, user.email);

                if(foundEmail != null)
                    foundUser = await services.provider.FindByIdAsync(foundEmail.entityId);
            }

            //Should this be the same as bad password? eeeehhhh
            if(foundUser == null)
                return BadRequest("Must provide a valid username or email!");
            
            if(foundUser.HasValue(keys.RegistrationCodeKey)) //There's a registration code pending
                return BadRequest("You must confirm your email first");

            var hash = hashService.GetHash(user.password, Convert.FromBase64String(foundUser.GetValue(keys.PasswordSaltKey).value));

            if(!hash.SequenceEqual(Convert.FromBase64String(foundUser.GetValue(keys.PasswordHashKey).value)))
                return BadRequest("Password incorrect!");

            return tokenService.GetToken(new Dictionary<string, string>()
            {
                { keys.UserIdentifier, ConvertToView(foundUser).id.ToString() }
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

            if(await services.provider.FindByNameBaseAsync(user.username) != null || await services.provider.FindValueAsync(keys.EmailKey, user.email) != null)
                return BadRequest("This user already seems to exist!");
            
            var salt = hashService.GetSalt();
            var fullUser = services.mapper.Map<UserViewFull>(user);

            fullUser.salt = Convert.ToBase64String(salt);
            fullUser.password = Convert.ToBase64String(hashService.GetHash(fullUser.password, salt));
            fullUser.registrationKey = Guid.NewGuid().ToString();

            await PostCleanAsync(fullUser);

            return ConvertToView(await WriteViewAsync(fullUser));
        }

        public class RegistrationEmailPost
        {
            public string email {get;set;}
        }

        [HttpPost("register/sendemail")]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]RegistrationEmailPost post)
        {
            var emailValue = await services.provider.FindValueAsync(keys.EmailKey, post.email);

            if(emailValue == null)
                return BadRequest("No user with that email");

            //Now look up the registration code (that's all we need from user)
            var registrationCode = await services.provider.FindValueAsync(keys.RegistrationCodeKey, null, emailValue.entityId);

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
        public async Task<ActionResult> ConfirmEmail([FromBody]ConfirmEmailPost post)
        {
            if(string.IsNullOrEmpty(post.confirmationKey))
                return BadRequest("Must provide a confirmation key in the body");

            var confirmValue = await services.provider.FindValueAsync(keys.RegistrationCodeKey, post.confirmationKey);

            if(confirmValue == null)
                return BadRequest("No user found with confirmation key");

            await services.provider.DeleteAsync(confirmValue);

            return Ok("Email Confirmed");
        }
        
    }
}