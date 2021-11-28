using System.Collections.Generic;
using System.Linq;
using contentapi.Security;
using Xunit;

namespace contentapi.test;

public class HashServiceTest : UnitTestBase
{
    protected HashService service;

    public HashServiceTest()
    {
        service = new HashService(GetService<HashServiceConfig>());
    }

    [Fact]
    public void GetSalt_DifferentSalts()
    {
        var salts = new List<byte[]>();

        //Make sure lots of different salts
        for(var i = 0; i < 20; i++)
        {
            var newSalt = service.GetSalt();

            Assert.All(salts, x =>
            {
                Assert.False(x.SequenceEqual(newSalt), "Same salt generated multiple times!");
            });

            salts.Add(newSalt);
        }
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("*$304m42390]][\\")]
    [InlineData("a")]
    [InlineData("😡emoji🥵😩🤖ë÷ʬ")]
    [InlineData("Well, I never!")]
    [InlineData("'this' or \"that\";")]
    public void VerifyText(string text)
    {
        var salt = service.GetSalt();
        var hash = service.GetHash(text, salt);
        Assert.True(service.VerifyText(text, hash, salt), $"Couldn't verify password {text}");
        Assert.False(service.VerifyText(text, hash, service.GetSalt()), $"Hash still succeeded with different salt! {text}");
    }
}