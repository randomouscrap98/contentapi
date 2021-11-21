namespace contentapi.Services
{
    public interface IHashService
    {
        byte[] GetSalt();
        byte[] GetHash(string text, byte[] salt);
        bool VerifyText(string text, byte[] hash, byte[] salt);
    }

}