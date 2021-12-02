using System.Security.Claims;

namespace contentapi.Security;

public interface IAuthTokenService<T> where T : struct
{
    /// <summary>
    /// Add only what YOU need to the data! This class already uses the data for tracking token validation, user id, etc.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="data"></param>
    /// <param name="expireOverride"></param>
    /// <returns></returns>
    string GetNewToken(T userId, Dictionary<string, string> data, TimeSpan? expireOverride = null);
    ClaimsPrincipal ValidateToken(string token);
    void InvalidateAllTokens(T userId);
    Dictionary<string, string> GetValuesFromClaims(IEnumerable<Claim> claims);

    /// <summary>
    /// Performs validation on the claims (using internal validation system applied on GetNewToken) and only returns a user id
    /// if the claim is valid BASED ON OUR STANDARDS! We don't check for expiration, as we assume someone else did
    /// </summary>
    /// <param name="claims"></param>
    /// <returns></returns>
    T? GetUserId(IEnumerable<Claim> claims); //Returns null if no VALID user found
}