using System.Security.Claims;

namespace contentapi;

public interface IAuthTokenService<T> where T : struct
{
    string GetNewToken(T userId, Dictionary<string, string> data, TimeSpan? expireOverride = null);
    void InvalidateAllTokens(T userId);
    Dictionary<string, string> GetValuesFromClaims(IEnumerable<Claim> claims);
    T? GetUserId(IEnumerable<Claim> claims); //Returns null if no VALID user found
    //bool ClaimStillValid(IEnumerable<Claim> claims);
    //Dictionary<string, string> GetBaseSecurityClaims(T userId);
}

    //public class TokenData
    //{
    //    public long Id {get;set;}
    //}
