using System.Collections.Generic;
using AutoMapper;
using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;

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
        public ActionResult<IEnumerable<UserView>> Get()
        {
            return context.Users.Select(x => mapper.Map<UserView>(x)).ToList();
        }

        [HttpPost]
        public ActionResult<UserView> Post([FromForm]UserCredential user)
        {
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
            context.SaveChanges();

            return mapper.Map<UserView>(newUser);
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