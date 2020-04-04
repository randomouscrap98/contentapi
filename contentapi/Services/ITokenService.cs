using System.Collections.Generic;

namespace contentapi.Services
{
    public class TokenData
    {
        public long Id {get;set;}
    }

    public interface ITokenService
    {
        string GetToken(Dictionary<string, string> data);
    }
}