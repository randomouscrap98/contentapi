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
    public class UsersController : GenericController<User, UserView> //, UserCredential>
    {
        public UsersController(GenericControllerServices services) : base (services) { }

        protected override void SetLogField(ActionLog log, long id) { log.userId = id; }

        protected override Task Post_PreConversionCheck(UserView user) //UserCredential user)
        {
            ThrowAction(BadRequest("Cannot create users from this endpoint right now! Use credentials endpoint"));
            return Task.CompletedTask;
            //await base.Post_PreConversionCheck(user);

            ////One day, fix these so they're the "standard" bad object request from model validation!!
            ////Perhaps do custom validation!
            //if(user.username == null)
            //    ThrowAction(BadRequest("Must provide a username!"));
            //if(user.email == null)
            //    ThrowAction(BadRequest("Must provide an email!"));

            //var existing = await services.context.GetAll<User>().FirstOrDefaultAsync(x => x.username == user.username || x.email == user.email);

            //if(existing != null)
            //    ThrowAction(BadRequest("This user already seems to exist!"));
        }

        //protected override User Post_ConvertItem(UserCredential user) 
        //{
        //    var salt = services.hash.GetSalt();

        //    return new User()
        //    {
        //        username = user.username,
        //        createDate = DateTime.Now,
        //        email = user.email,
        //        passwordSalt = salt,
        //        passwordHash = services.hash.GetHash(user.password, salt),
        //        registerCode = Guid.NewGuid().ToString()
        //    };
        //}

        protected override Task Delete_PreDeleteCheck(User existing)
        {
            //NOBODY can delete users right now because that's a HUGE cascading thing that I'm not implementing right now!
            ThrowAction(BadRequest("Deleting users not supported right now!"));
            return Task.CompletedTask; //just to satisfy the compiler

            //var me = await GetCurrentUserAsync();

            //if(!services.permission.CanDo(me.role, Permission.DeleteUser))
            //    ThrowAction(Unauthorized("You don't have permission to delete users!"));
        }

        //Don't support changing username/password/email yet (it's kind of a big process)
        protected override Task Put_PreConversionCheck(UserView item, User existing)
        {
            ThrowAction(BadRequest("You can't change user data yet! Sorry!"));
            return Task.CompletedTask;
        }

        //You 'Create' a new user by posting ONLY 'credentials'. This is different than most other types of things...
        //passwords and emails and such shouldn't be included in every view unlike regular models where every field is there.
        [HttpPost("credentials")]
        public async Task<ActionResult<UserView>> PostCredentials([FromBody]UserCredential user)
        {
            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                return BadRequest("Must provide a username!");
            if(user.email == null)
                return BadRequest("Must provide an email!");

            var existing = await services.context.GetAll<User>().FirstOrDefaultAsync(x => x.username == user.username || x.email == user.email);

            if(existing != null)
                return BadRequest("This user already seems to exist!");

            var salt = services.hash.GetSalt();

            var createUser = new User()
            {
                username = user.username,
                createDate = DateTime.Now,
                email = user.email,
                passwordSalt = salt,
                passwordHash = services.hash.GetHash(user.password, salt),
                registerCode = Guid.NewGuid().ToString()
            };

            await services.context.Set<User>().AddAsync(createUser);
            await services.context.SaveChangesAsync();

            await LogAct(LogAction.Create, createUser.id);

            return services.mapper.Map<UserView>(createUser);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserView>> Me()
        {
            try
            {
                return await GetSingle(services.session.GetCurrentUid());
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Need to override to allow anonymous
        //[AllowAnonymous]
        //public async override Task<ActionResult<UserView>> Post([FromBody]UserCredential item)
        //{
        //    return await base.Post(item);
        //}

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
            var foundUser = await services.context.GetAll<User>().FirstOrDefaultAsync(x => x.email == data.email);

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

            var users = services.context.GetAll<User>();
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
            var users = services.context.GetAll<User>();

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