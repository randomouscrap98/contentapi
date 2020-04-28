using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace contentapi.Services.Implementations
{
    public class TokenServiceConfig
    {
        public TimeSpan TokenExpiration {get;set;} = TimeSpan.FromDays(60);
        public string SecretKey {get;set;} = null;
    }

    public class TokenService : ITokenService
    {
        public string UserIdField = "uid";
        public TokenServiceConfig config;

        public TokenService(TokenServiceConfig config)
        {
            this.config = config;
        }

        //protected string ProcessFieldValue(string field, string value)
        //{
        //    if(value == null)
        //        throw new InvalidOperationException($"No {field} field in User! Maybe there was no auth?");

        //    return value;
        //}

        //public virtual string GetCurrentField(string field)
        //{
        //    if(Context.User == null)
        //        throw new InvalidOperationException("User is not set! Maybe there was no auth?");

        //    return ProcessFieldValue(field, Context.User.FindFirstValue(field));
        //}

        //public virtual long GetCurrentUid()
        //{
        //    try
        //    {
        //        return long.Parse(GetCurrentField(UserIdField));
        //    }
        //    catch(Exception)
        //    {
        //        //TODO: LOGGING GOES HERE!
        //        return -1;
        //    }
        //}

        public string GetToken(Dictionary<string, string> tokenData, TimeSpan? expireOverride = null)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(tokenData.Select(x => new Claim(x.Key, x.Value)).ToArray()),
                Expires = DateTime.UtcNow.Add(expireOverride ?? config.TokenExpiration),
                NotBefore = DateTime.UtcNow.AddMinutes(-30),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(config.SecretKey)), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();
            var token = handler.CreateToken(descriptor);
            var tokenString = handler.WriteToken(token);
            return tokenString;
        }
    }

}