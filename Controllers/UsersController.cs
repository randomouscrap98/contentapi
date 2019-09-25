using System.Collections.Generic;
using AutoMapper;
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
using contentapi.Services;

namespace contentapi.Controllers
{
    public class UsersControllerConfig
    {
        public int SaltBits = 256;
        public int HashBits = 512;
        public int HashIterations = 10000;

        public TimeSpan TokenExpiration = TimeSpan.FromDays(60);

        public string JwtSecretKey = "nothing";
    }

    public class EmailConfig
    {
        public string Host;
        public string User;
        public string Password;
        public int Port;

        public string SubjectFront;
    }

    public class UsersController : GenericControllerRaw<User, UserView, UserCredential>
    {
        protected UsersControllerConfig config;
        protected EmailConfig emailConfig;

        public UsersController(ContentDbContext context, IMapper mapper, PermissionService permissionService, UsersControllerConfig config, EmailConfig emailConfig) : 
            base (context, mapper, permissionService)
        {
            this.config = config;
            this.emailConfig = emailConfig;
        }

        protected override async Task Post_PreConversionCheck(UserCredential user)
        {
            await base.Post_PreConversionCheck(user);

            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                ThrowAction(BadRequest("Must provide a username!"));
            if(user.email == null)
                ThrowAction(BadRequest("Must provide an email!"));

            var existing = await context.GetAll<User>().FirstOrDefaultAsync(x => x.username == user.username || x.email == user.email);

            if(existing != null)
                ThrowAction(BadRequest("This user already seems to exist!"));
        }

        protected override User Post_ConvertItem(UserCredential user) 
        {
            var salt = GetSalt();

            return new User()
            {
                username = user.username,
                createDate = DateTime.Now,
                email = user.email,
                passwordSalt = salt,
                passwordHash = GetHash(user.password, salt),
                registerCode = Guid.NewGuid().ToString()
            };
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserView>> Me()
        {
            try
            {
                return await GetSingle(GetCurrentUid());
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

        [HttpPost("sendemail")]
        [AllowAnonymous]
        public async Task<ActionResult> SendRegistrationEmail([FromBody]RegistrationData data)
        {
            var foundUser = await context.GetAll<User>().FirstOrDefaultAsync(x => x.email == data.email);

            if(foundUser == null)
                return BadRequest("No user with that email");
            if(foundUser.registerCode == null)
                return BadRequest("Nothing to do for user");

            using(var message = new MailMessage())
            {
                message.To.Add(new MailAddress(data.email));
                message.From = new MailAddress(emailConfig.User);
                message.Subject = $"{emailConfig.SubjectFront} - Confirm Email";
                message.Body = $"Your confirmation code is {foundUser.registerCode}";

                using(var client = new SmtpClient(emailConfig.Host))
                {
                    client.Port = emailConfig.Port;
                    client.Credentials = new NetworkCredential(emailConfig.User, emailConfig.Password);
                    client.EnableSsl = true;
                    client.Send(message);
                }
            }

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

            var users = context.GetAll<User>();
            var foundUser = await users.FirstOrDefaultAsync(x => x.registerCode == data.confirmationKey);

            if(foundUser == null)
                return BadRequest("No user found with confirmation key");

            foundUser.registerCode = null;

            context.Set<User>().Update(foundUser);
            await context.SaveChangesAsync();

            return Ok("Email Confirmed");
        }

        [HttpPost("authenticate")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserCredential user)
        {
            User foundUser = null;
            var users = context.GetAll<User>();

            if(user.username != null)
                foundUser = await users.FirstOrDefaultAsync(x => x.username == user.username);
            else if (user.email != null)
                foundUser = await users.FirstOrDefaultAsync(x => x.email == user.email);

            //Should this be the same as bad password? eeeehhhh
            if(foundUser == null)
                return BadRequest("Must provide a valid username or email!");

            if(foundUser.registerCode != null)
                return BadRequest("You must confirm your email first");

            var hash = GetHash(user.password, foundUser.passwordSalt);

            if(!hash.SequenceEqual(foundUser.passwordHash))
                return BadRequest("Password incorrect!");

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[] {
                    new Claim("uid", foundUser.id.ToString()) }),
                    //new Claim("role", foundUser.role.ToString("G")) }),
                Expires = DateTime.UtcNow.Add(config.TokenExpiration),
                NotBefore = DateTime.UtcNow.AddMinutes(-30),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(config.JwtSecretKey)), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();
            var token = handler.CreateToken(descriptor);
            var tokenString = handler.WriteToken(token);
            return tokenString;
        }

        protected byte[] GetSalt()
        {
            byte[] salt = new byte[ config.SaltBits / 8 ];

            using(var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return salt;
        }

        protected byte[] GetHash(string password, byte[] salt)
        {
            return KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: config.HashIterations,
                numBytesRequested: config.HashBits / 8
            );
        }

        protected bool VerifyPassword(string password, byte[] hash, byte[] salt)
        {
            var newHash = GetHash(password, salt);
            return hash.SequenceEqual(newHash);
        }
    }
}