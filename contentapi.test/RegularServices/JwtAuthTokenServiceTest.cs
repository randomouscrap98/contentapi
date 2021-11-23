using System.Collections.Generic;
using contentapi.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace contentapi.test;

public class JwtAuthTokenServiceTest : UnitTestBase
{
    public const string DefaultSecretKey = "defaultKeyNeedsToBeLong";
    protected JwtAuthTokenService<long> service;

    public JwtAuthTokenServiceTest()
    {
        service = new JwtAuthTokenService<long>(GetService<ILogger<JwtAuthTokenService<long>>>(), 
            new JwtAuthTokenServiceConfig(), GetNewCredentials(DefaultSecretKey),
            GetNewValidationParameters(DefaultSecretKey));
    }

    protected SigningCredentials GetNewCredentials(string secretKey)
    {
        return new SigningCredentials(
            new SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(secretKey)), 
            SecurityAlgorithms.HmacSha256Signature);
    }

    protected TokenValidationParameters GetNewValidationParameters(string secretKey)
    {
        return new TokenValidationParameters()
        {
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false
                //tokenSection.GetValue<string>("SecretKey"))),
        };
    }

    [Fact]
    public void GetNewTokenTest_NoException()
    {
        var key = service.GetNewToken(55, new Dictionary<string, string>());
        Assert.True(!string.IsNullOrWhiteSpace(key));
        var key2 = service.GetNewToken(56, new Dictionary<string, string>());
        Assert.NotEqual(key, key2);
    }

    [Fact]
    public void GetUserId_StillValid()
    {
        var key = service.GetNewToken(55, new Dictionary<string, string>());
        var claims = service.ValidateToken(key);
        Assert.NotNull(claims);
        var userId = service.GetUserId(claims.Claims);
        Assert.Equal(55, userId);
    }
}