using System.Linq;
using System.Security.Cryptography;
using contentapi.Configs;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace contentapi.Services
{
    public interface IHashService
    {
        byte[] GetSalt();
        byte[] GetHash(string text, byte[] salt);
        bool VerifyText(string text, byte[] hash, byte[] salt);
    }

    public class HashService : IHashService
    {
        public HashConfig Config;

        public HashService(HashConfig config)
        {
            this.Config = config;
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