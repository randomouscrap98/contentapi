using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using contentapi.Configs;
using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace contentapi.Services
{
    public interface ISessionService
    {
        ControllerBase Context {get;set;}

        string GetCurrentField(string field);
        long GetCurrentUid();
        string GetToken(User user);
    }

    public class SessionService : ISessionService
    {
        public ControllerBase Context {get;set;}

        public string UserIdField = "uid";
        public SessionConfig config;

        public SessionService(SessionConfig config)
        {
            this.config = config;
        }

        protected string ProcessFieldValue(string field, string value)
        {
            if(value == null)
                throw new InvalidOperationException($"No {field} field in User! Maybe there was no auth?");

            return value;
        }

        public virtual string GetCurrentField(string field)
        {
            if(Context.User == null)
                throw new InvalidOperationException("User is not set! Maybe there was no auth?");

            return ProcessFieldValue(field, Context.User.FindFirstValue(field));
        }

        public virtual long GetCurrentUid()
        {
            try
            {
                return long.Parse(GetCurrentField(UserIdField));
            }
            catch(Exception)
            {
                //TODO: LOGGING GOES HERE!
                return -1;
            }
        }

        public string GetToken(User user)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[] {
                    new Claim(UserIdField, user.id.ToString()) }),
                    //new Claim("role", foundUser.role.ToString("G")) }),
                Expires = DateTime.UtcNow.Add(config.TokenExpiration),
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