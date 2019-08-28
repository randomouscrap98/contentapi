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

    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private ContentDbContext context;
        private IMapper mapper;

        protected UsersControllerConfig config;

        public UsersController(ContentDbContext context, IMapper mapper, UsersControllerConfig config)
        {
            this.context = context;
            this.mapper = mapper;
            this.config = config;
        }

        [HttpGet]
        public async Task<ActionResult<Object>> Get()
        {
            //Find a way to "fix" these results so you can do fancy sorting/etc.
            //Will we need this on every endpoint? Won't that be disgusting? How do we
            //make that "restful"? Look up pagination in REST
            return new { 
                users = await context.Users.Select(x => mapper.Map<UserView>(x)).ToListAsync(),
                _links = new List<string>(), //one day, turn this into HATEOS
                _claims = User.Claims.ToDictionary(x => x.Type, x => x.Value)
            };
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserView>> GetSingle(long id)
        {
            var user = await context.Users.FindAsync(id);

            if(user == null)
                return NotFound();

            return mapper.Map<UserView>(user);
        }

        //[AcceptVerbs("Post")]
        //public IActionResult CheckUniqueUsername(string username, int ? id)
        //{
        //    if(context.Users.FirstOrDefaultAsync(x => x.username == username) != null)
        //        return Json($"");
        //    return Json(true);
        //    );
        //}

        [HttpPost]
        public async Task<ActionResult<UserView>> Post([FromBody]UserCredential user)
        {
            //One day, fix these so they're the "standard" bad object request from model validation!!
            //Perhaps do custom validation!
            if(user.username == null)
                return BadRequest("Must provide a username!");
            if(user.email == null)
                return BadRequest("Must provide an email!");

            var existing = await context.Users.FirstOrDefaultAsync(
                x => x.username == user.username || x.email == user.email);

            if(existing != null)
                return BadRequest("This user already seems to exist!");

            var salt = GetSalt();

            var newUser = new User()
            {
                username = user.username,
                createDate = DateTime.Now,
                email = user.email,
                passwordSalt = salt,
                passwordHash = GetHash(user.password, salt)
            };

            context.Users.Add(newUser);
            await context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSingle), new { id = newUser.id }, mapper.Map<UserView>(newUser));
        }

        [HttpPost("authenticate")]
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