using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using contentapi.Services;

namespace contentapi.Controllers
{
    public class UsersController : EntityController<User, UserView>
    {
        public UsersController(EntityControllerServices services) : base (services) { }

        protected override Task<User> Post_ConvertItemAsync(UserView view)
        {
            ThrowAction(BadRequest("Cannot create users from this endpoint right now! Use credentials endpoint"));
            throw new NotImplementedException(); //This will hopefully never be reached
        }

        protected override Task Delete_PrecheckAsync(User existing)
        {
            //NOBODY can delete users right now because that's a HUGE cascading thing that I'm not implementing right now!
            ThrowAction(BadRequest("Deleting users not supported right now!"));
            return Task.CompletedTask;
        }

        //Don't support changing username/password/email yet (it's kind of a big process)
        protected override Task Put_ConvertItemAsync(UserView item, User existing)
        {
            ThrowAction(BadRequest("You can't change user data yet! Sorry!"));
            return Task.CompletedTask;
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

            var all = await GetAllBase();
            var existing = await all.FirstOrDefaultAsync(x => x.username == user.username || x.email == user.email);

            if(existing != null)
                return BadRequest("This user already seems to exist!");

            var salt = services.hash.GetSalt();

            var createUser = new User()
            {
                username = user.username,
                email = user.email,
                passwordSalt = salt,
                passwordHash = services.hash.GetHash(user.password, salt),
                registerCode = Guid.NewGuid().ToString()
            };

            services.entity.SetNewEntity(createUser);

            //Everyone can read the user information (not the secret stuff of course)
            createUser.Entity.baseAllow = EntityAction.Read;

            await services.context.Set<User>().AddAsync(createUser);
            await services.context.SaveChangesAsync();

            //Note the last parameter: the create user is ALWAYS the user that just got created! The user "creates" itself!
            await LogAct(EntityAction.Create, createUser.entityId, createUser.entityId);

            return services.entity.ConvertFromEntity<User, UserView>(createUser); //services.mapper.Map<UserView>(createUser);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserView>> Me()
        {
            var uid = services.session.GetCurrentUid();

            if(uid <= 0)
                return BadRequest("Not logged in!");

            return await GetSingle(services.session.GetCurrentUid());
        }

        //Temp class stuff to make life... easier?
        public class RegistrationData
        {
            public string email {get;set;}
        }

        public virtual async Task SendConfirmationEmailAsync(string recipient, string code)
        {
            var subject = services.language.GetString("ConfirmEmailSubject", "en");
            var body = services.language.GetString("ConfirmEmailBody", "en", new Dictionary<string, object>() {{"confirmCode", code}});
            await services.email.SendEmailAsync(new EmailMessage(recipient, subject, body));
        }

        [HttpPost("sendemail")]
        [AllowAnonymous]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]RegistrationData data)
        {
            var foundUser = await (await GetAllBase()).FirstOrDefaultAsync(x => x.email == data.email);

            if(foundUser == null)
                return BadRequest("No user with that email");
            if(foundUser.registerCode == null)
                return BadRequest("Nothing to do for user");

            await SendConfirmationEmailAsync(data.email, foundUser.registerCode);

            return Ok("Email sent");
        }

        //Temp class stuff to make life... easier?
        public class ConfirmationData
        {
            public string confirmationKey {get;set;}
        }

        [HttpPost("confirm")]
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail([FromBody]ConfirmationData data)
        {
            if(string.IsNullOrEmpty(data.confirmationKey))
                return BadRequest("Must provide a confirmation key in the body");

            var users = await GetAllBase(); //services.context.GetAll<User>();
            var foundUser = await users.FirstOrDefaultAsync(x => x.registerCode == data.confirmationKey);

            if(foundUser == null)
                return BadRequest("No user found with confirmation key");

            foundUser.registerCode = null;

            services.context.Set<User>().Update(foundUser);
            await services.context.SaveChangesAsync();

            return Ok("Email Confirmed");
        }

        [HttpPost("authenticate")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserCredential user)
        {
            User foundUser = null;
            var users = await GetAllBase(); //services.context.GetAll<User>();

            if(user.username != null)
                foundUser = await users.FirstOrDefaultAsync(x => x.username == user.username);
            else if (user.email != null)
                foundUser = await users.FirstOrDefaultAsync(x => x.email == user.email);

            //Should this be the same as bad password? eeeehhhh
            if(foundUser == null)
                return BadRequest("Must provide a valid username or email!");

            if(foundUser.registerCode != null)
                return BadRequest("You must confirm your email first");

            var hash = services.hash.GetHash(user.password, foundUser.passwordSalt);

            if(!hash.SequenceEqual(foundUser.passwordHash))
                return BadRequest("Password incorrect!");

            return services.session.GetToken(foundUser);
        }
    }
}