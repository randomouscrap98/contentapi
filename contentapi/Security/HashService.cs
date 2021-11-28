using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace contentapi.Security;

public class HashServiceConfig
{
    public int SaltBits = 256;
    public int HashBits = 512;
    public int HashIterations = 10000;
}

public class HashService : IHashService
{
    public HashServiceConfig Config;

    public HashService(HashServiceConfig config)
    {
        this.Config = config;
    }

    public byte[] GetSalt()
    {
        byte[] salt = new byte[Config.SaltBits / 8];

        using (var rng = RandomNumberGenerator.Create())
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
