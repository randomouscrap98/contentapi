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

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        public int SaltBits = 256;
        public int HashBits = 512;
        public int HashIterations = 10000;

        private ContentDbContext context;
        private IMapper mapper;

        public UsersController(ContentDbContext context, IMapper mapper)
        {
            this.context = context;
            this.mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserView>>> Get()
        {
            //Find a way to "fix" these results so you can do fancy sorting/etc.
            //Will we need this on every endpoint? Won't that be disgusting? How do we
            //make that "restful"? Look up pagination in REST
            return await context.Users.Select(x => mapper.Map<UserView>(x)).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserView>> GetSingle(long id)
        {
            var user = await context.Users.FindAsync(id);

            if(user == null)
                return NotFound();

            return mapper.Map<UserView>(user);
        }

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

        protected byte[] GetSalt()
        {
            byte[] salt = new byte[ SaltBits / 8 ];

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
                iterationCount: HashIterations,
                numBytesRequested: HashBits / 8
            );
        }

        protected bool VerifyPassword(string password, byte[] hash, byte[] salt)
        {
            var newHash = GetHash(password, salt);
            return hash.SequenceEqual(newHash);
        }
    }
}