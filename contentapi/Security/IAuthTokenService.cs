using System.Security.Claims;

namespace contentapi.Security;

public interface IAuthTokenService<T> where T : struct
{
    string GetNewToken(T userId, Dictionary<string, string> data, TimeSpan? expireOverride = null);
    ClaimsPrincipal ValidateToken(string token);
    void InvalidateAllTokens(T userId);
    Dictionary<string, string> GetValuesFromClaims(IEnumerable<Claim> claims);
    T? GetUserId(IEnumerable<Claim> claims); //Returns null if no VALID user found
}