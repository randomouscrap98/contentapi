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

    public class UsersController : GenericControllerRaw<User, UserView, UserCredential>
    {
        protected UsersControllerConfig config;

        public UsersController(ContentDbContext context, IMapper mapper, UsersControllerConfig config) : base (context, mapper)
        {
            this.config = config;
        }

        public override DbSet<User> GetObjects() { return context.Users; }
        
        protected override async Task Post_PreConversionCheck(UserCredential user)
        {
            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                ThrowAction(BadRequest("Must provide a username!"));
            if(user.email == null)
                ThrowAction(BadRequest("Must provide an email!"));

            var existing = await context.Users.FirstOrDefaultAsync(x => x.username == user.username || x.email == user.email);

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
                passwordHash = GetHash(user.password, salt)
            };
        }

        [AllowAnonymous]
        public async override Task<ActionResult<UserView>> Post([FromBody]UserCredential item)
        {
            return await base.Post(item);
        }

        [HttpPost("authenticate")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> Authenticate([FromBody]UserCredential user)
        {
            User foundUser = null;

            if(user.username != null)
                foundUser = await context.Users.FirstOrDefaultAsync(x => x.username == user.username);
            else if (user.email != null)
                foundUser = await context.Users.FirstOrDefaultAsync(x => x.email == user.email);

            //Should this be the same as bad password? eeeehhhh
            if(foundUser == null)
                return BadRequest("Must provide a valid username or email!");

            var hash = GetHash(user.password, foundUser.passwordSalt);

            if(!hash.SequenceEqual(foundUser.passwordHash))
                return BadRequest("Password incorrect!");

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[] {
                    new Claim("uid", foundUser.id.ToString()) }),
                Expires = DateTime.UtcNow.Add(config.TokenExpiration),
                NotBefore = DateTime.UtcNow.AddMinutes(-30),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(config.JwtSecretKey)), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var handler = new JwtSecurityTokenHandler();
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