using System.Collections.Generic;
using contentapi.Services.Implementations;
using Xunit;

namespace contentapi.test
{
    public class TokenServiceTests : ServiceConfigTestBase<TokenService, TokenServiceConfig>
    {
        protected override TokenServiceConfig config { get => new TokenServiceConfig() {SecretKey = 
            "whydoesthisstringhavetobesodanglong"};}

        [Fact]
        public void CreateToken()
        {
            var token = service.GetToken(new Dictionary<string, string>());
            Assert.NotEmpty(token);
        }

        [Fact]
        public void CreateTokenWithFields()
        {
            var token = service.GetToken(new Dictionary<string, string>()
            {
                { "uid", "542" }
            });

            Assert.NotEmpty(token);
        }

        [Fact]
        public void TokenWithFieldsChanges()
        {
            var token = service.GetToken(new Dictionary<string, string>());
            var token2 = service.GetToken(new Dictionary<string, string>()
            {
                { "uid", "542" }
            });

            Assert.NotEqual(token, token2);
        }
    }
}