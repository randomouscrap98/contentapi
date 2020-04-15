using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;

namespace contentapi.Services.Implementations
{
    public class HashConfig
    {
        public int SaltBits = 256;
        public int HashBits = 512;
        public int HashIterations = 10000;
    }

    public class HashService : IHashService
    {
        public HashConfig Config;

        public HashService(IOptionsMonitor<HashConfig> config)
        {
            this.Config = config.CurrentValue;
        }

        public byte[] GetSalt()
        {
            byte[] salt = new byte[ Config.SaltBits / 8 ];

            using(var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return salt;
        }

        public byte[] GetHash(string text, byte[] salt)
        {
            return KeyDerivation.Pbkdf2(
                password: text,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: Config.HashIterations,
                numBytesRequested: Config.HashBits / 8
            );
        }

        public bool VerifyText(string text, byte[] hash, byte[] salt)
        {
            var newHash = GetHash(text, salt);
            return hash.SequenceEqual(newHash);
        }
    }
}