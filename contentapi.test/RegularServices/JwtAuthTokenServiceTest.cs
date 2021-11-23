using System;
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
        service = GetService(DefaultSecretKey);
    }

    protected JwtAuthTokenService<long> GetService(string key)
    {
        return new JwtAuthTokenService<long>(GetService<ILogger<JwtAuthTokenService<long>>>(), 
            new JwtAuthTokenServiceConfig(), GetNewCredentials(key),
            GetNewValidationParameters(key));
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

    [Fact]
    public void GetNewTokenTest_ValuesStored()
    {
        var key = service.GetNewToken(55, new Dictionary<string, string>()
        {
            { "key1",  "hahakey" },
            { "thing", "bunnies" }
        });
        var principal = service.ValidateToken(key);
        Assert.NotEmpty(principal.Claims);
        var values = service.GetValuesFromClaims(principal.Claims);
        Assert.Equal("hahakey", values["key1"]);
        Assert.Equal("bunnies", values["thing"]);
    }

    [Fact]
    public void GetUserId_NotModifiable()
    {
        var service2 = GetService("someOtherKeyThatIsNoTTHESAME");
        var key = service.GetNewToken(55, new Dictionary<string, string>());
        var key2 = service2.GetNewToken(56, new Dictionary<string, string>());
        //The bad claims
        Assert.ThrowsAny<Exception>(() => service2.ValidateToken(key));
        Assert.ThrowsAny<Exception>(() => service.ValidateToken(key2));
        //var claims = service2.ValidateToken(key);
        //var claims2 = service.ValidateToken(key2);
        //Assert.Empty(claims.Claims);
        //Assert.Empty(claims2.Claims);
        //The good claims
        //var claims = service.ValidateToken(key);
        //var claims2 = service2.ValidateToken(key2);
        //Assert.NotEmpty(claims.Claims);
        //Assert.NotEmpty(claims2.Claims);
        //var userId = service2.GetUserId(claims.Claims);
        //var userId2 = service.GetUserId(claims2.Claims);
        //Assert.Null(userId);
        //Assert.Null(userId2);
    }

    [Fact]
    public void GetUserId_Invalidate()
    {
        var key = service.GetNewToken(55, new Dictionary<string, string>());
        service.InvalidateAllTokens(55);
        var claims = service.ValidateToken(key);
        var userId = service.GetUserId(claims.Claims);
        Assert.Null(userId);
    }
}