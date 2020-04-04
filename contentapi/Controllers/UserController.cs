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

namespace contentapi.Controllers
{
    public class UserController : ControllerBase
    {
        protected ILogger<UserController> logger;
        protected IHashService hashService;
        protected IEntityProvider entityProvider;

        public const string EmailKey = "se";
        public const string PasswordHashKey = "sph";
        public const string PasswordSaltKey = "sps";
        public const string RegistrationCodeKey = "srk";

        public UserController(ILogger<UserController> logger, IHashService hashService, IEntityProvider entityProvider) 
        { 
            this.logger = logger;
            this.hashService = hashService;
            this.entityProvider = entityProvider;
        }

        //You 'Create' a new user by posting ONLY 'credentials'. This is different than most other types of things...
        //passwords and emails and such shouldn't be included in every view unlike regular models where every field is there.
        [HttpPost("credentials")]
        [AllowAnonymous]
        public async Task<ActionResult<UserView>> PostCredentials([FromBody]UserCredential user)
        {
            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                return BadRequest("Must provide a username!");
            if(user.email == null)
                return BadRequest("Must provide an email!");

            var existingByUsername = await entityProvider.GetEntitiesAsync(new EntitySearch() { NameLike = user.username});
            var existingByEmail = await entityProvider.GetEntityValuesAsync(new EntityValueSearch() { KeyLike = EmailKey, ValueLike = user.email});

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

            var view = new UserView()
            {
                id = newUser.Entity.id,
                createDate = newUser.Entity.createDate,
                username = newUser.Entity.name,
                email = newUser.Values[EmailKey].First().value //We're the creator so we get to see the email
            };

            return view;
            //return services.entity.ConvertFromEntity<UserEntity, UserView>(createUser); //services.mapper.Map<UserView>(createUser);
        }

        //[HttpGet("me")]
        //public async Task<ActionResult<UserView>> Me()
        //{
        //    var uid = services.session.GetCurrentUid();

        //    if(uid <= 0)
        //        return BadRequest("Not logged in!");

        //    return await GetSingle(services.session.GetCurrentUid());
        //}

        //Temp class stuff to make life... easier?
        public class RegistrationData
        {
            public string email {get;set;}
        }

        //public virtual async Task SendConfirmationEmailAsync(string recipient, string code)
        //{
        //    var subject = services.language.GetString("ConfirmEmailSubject", "en");
        //    var body = services.language.GetString("ConfirmEmailBody", "en", new Dictionary<string, object>() {{"confirmCode", code}});
        //    await services.email.SendEmailAsync(new EmailMessage(recipient, subject, body));
        //}

        [HttpPost("sendemail")]
        [AllowAnonymous]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]RegistrationData data)
        {
            var foundUser = await (await GetAllReadableAsync()).FirstOrDefaultAsync(x => x.email == data.email);

            if(foundUser == null)
                return BadRequest("No user with that email");
            if(foundUser.registerCode == null)
                return BadRequest("Nothing to do for user");

            await SendConfirmationEmailAsync(data.email, foundUser.registerCode);

            return Ok("Email sent");
        }

        ////Temp class stuff to make life... easier?
        //public class ConfirmationData
        //{
        //    public string confirmationKey {get;set;}
        //}

        //[HttpPost("confirm")]
        //[AllowAnonymous]
        //public async Task<ActionResult> ConfirmEmail([FromBody]ConfirmationData data)
        //{
        //    if(string.IsNullOrEmpty(data.confirmationKey))
        //        return BadRequest("Must provide a confirmation key in the body");

        //    var users = await GetAllReadableAsync(); //services.context.GetAll<User>();
        //    var foundUser = await users.FirstOrDefaultAsync(x => x.registerCode == data.confirmationKey);

        //    if(foundUser == null)
        //        return BadRequest("No user found with confirmation key");

        //    foundUser.registerCode = null;

        //    services.context.Set<UserEntity>().Update(foundUser);
        //    await services.context.SaveChangesAsync();

        //    return Ok("Email Confirmed");
        //}

        //[HttpPost("authenticate")]
        //[AllowAnonymous]
        //public async Task<ActionResult<string>> Authenticate([FromBody]UserCredential user)
        //{
        //    UserEntity foundUser = null;
        //    var users = await GetAllReadableAsync(); //services.context.GetAll<User>();

        //    if(user.username != null)
        //        foundUser = await users.FirstOrDefaultAsync(x => x.username == user.username);
        //    else if (user.email != null)
        //        foundUser = await users.FirstOrDefaultAsync(x => x.email == user.email);

        //    //Should this be the same as bad password? eeeehhhh
        //    if(foundUser == null)
        //        return BadRequest("Must provide a valid username or email!");

        //    if(foundUser.registerCode != null)
        //        return BadRequest("You must confirm your email first");

        //    var hash = services.hash.GetHash(user.password, foundUser.passwordSalt);

        //    if(!hash.SequenceEqual(foundUser.passwordHash))
        //        return BadRequest("Password incorrect!");

        //    return services.session.GetToken(foundUser);
        //}
    }
}