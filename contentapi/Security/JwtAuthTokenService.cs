using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace contentapi.Security;

public class JwtAuthTokenServiceConfig
{
    public string UserIdField = "uid";
    public string UserTokenValidateField = "uvalid";
    public TimeSpan TokenExpiration {get;set;} = TimeSpan.FromDays(60);
}

public class JwtAuthTokenService<T> : IAuthTokenService<T> where T : struct
{
    public JwtAuthTokenServiceConfig config;
    protected SigningCredentials credentials;
    protected TokenValidationParameters validationParameters;
    protected ILogger logger;

    protected Dictionary<T, int> userTokenValidationTracking = new Dictionary<T, int>();
    protected readonly object validationLock = new object();

    public JwtAuthTokenService(ILogger<JwtAuthTokenService<T>> logger, JwtAuthTokenServiceConfig config, 
        SigningCredentials credentials, TokenValidationParameters validationParameters)
    {
        this.config = config;
        this.credentials = credentials;
        this.logger = logger;
        this.validationParameters = validationParameters;
    }

    protected int GetUserValidationTracking(T userId)
    {
        lock(validationLock)
        {
            if(!userTokenValidationTracking.ContainsKey(userId))
                userTokenValidationTracking.Add(userId, 0);

            return userTokenValidationTracking[userId];
        }
    }

    public string GetNewToken(T userId, Dictionary<string, string> tokenData, TimeSpan? expireOverride = null)
    {
        tokenData.Add(config.UserIdField, userId.ToString() ?? throw new InvalidOperationException($"TOSTRING FAILED ON USERID {userId}"));

        lock(validationLock)
        {
            tokenData.Add(config.UserTokenValidateField, GetUserValidationTracking(userId).ToString());
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(tokenData.Select(x => new Claim(x.Key, x.Value)).ToArray()),
            Expires = DateTime.UtcNow.Add(expireOverride ?? config.TokenExpiration),
            NotBefore = DateTime.UtcNow.AddMinutes(-30),
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        var token = handler.CreateToken(descriptor);
        var tokenString = handler.WriteToken(token);
        return tokenString;
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        SecurityToken rawToken;
        var principal = handler.ValidateToken(token, validationParameters, out rawToken);
        return principal; //not sure if we need the token...
    }

    public void InvalidateAllTokens(T userId)
    {
        lock(validationLock)
        {
            //This will make it such that attempting to get the user id will fail 
            //because the validator is different
            var original = GetUserValidationTracking(userId);
            userTokenValidationTracking[userId] = original + 1;
        }
    }

    public Dictionary<string, string> GetValuesFromClaims(IEnumerable<Claim> claims)
    {
        return claims.ToDictionary(x => x.Type, x => x.Value);
    }

    public T? GetUserId(IEnumerable<Claim> claims)
    {
        var convertedValues = GetValuesFromClaims(claims);
        lock(validationLock)
        {
            if(convertedValues.ContainsKey(config.UserIdField))
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                var userId = (T)(converter.ConvertFromString(convertedValues[config.UserIdField]) ?? throw new InvalidOperationException());
                if(convertedValues.ContainsKey(config.UserTokenValidateField) &&
                   convertedValues[config.UserTokenValidateField] == GetUserValidationTracking(userId).ToString())
                {
                    return userId;
                }
            }
        }
        return null;
    }
}