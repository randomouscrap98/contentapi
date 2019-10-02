using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Net.Mail;
using System.Net;
using contentapi.Configs;
using System.Collections.Generic;
using contentapi.Services;

namespace contentapi.Controllers
{
    public class UsersController : GenericControllerRaw<User, UserView, UserCredential>
    {
        public UsersController(GenericControllerServices services) : base (services) { }

        protected override void SetLogField(ActionLog log, long id) { log.userId = id; }

        protected override async Task Post_PreConversionCheck(UserCredential user)
        {
            await base.Post_PreConversionCheck(user);

            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                ThrowAction(BadRequest("Must provide a username!"));
            if(user.email == null)
                ThrowAction(BadRequest("Must provide an email!"));

            var existing = await services.context.GetAll<User>().FirstOrDefaultAsync(x => x.username == user.username || x.email == user.email);

            if(existing != null)
                ThrowAction(BadRequest("This user already seems to exist!"));
        }

        protected override User Post_ConvertItem(UserCredential user) 
        {
            var salt = services.hash.GetSalt();

            return new User()
            {
                username = user.username,
                createDate = DateTime.Now,
                email = user.email,
                passwordSalt = salt,
                passwordHash = services.hash.GetHash(user.password, salt),
                registerCode = Guid.NewGuid().ToString()
            };
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

        [AllowAnonymous]
        public async override Task<ActionResult<UserView>> Post([FromBody]UserCredential item)
        {
            return await base.Post(item);
        }

        //Temp class stuff to make life... easier?
        public class RegistrationData
        {
            public string email {get;set;}
        }

        //Override this to do custom email sending... or something.
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