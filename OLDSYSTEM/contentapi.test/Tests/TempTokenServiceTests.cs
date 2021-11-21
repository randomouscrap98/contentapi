using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Implementations;
using Xunit;

namespace contentapi.test
{
    public class TempTokenServiceTests : ServiceConfigTestBase<TempTokenService<long>, TempTokenServiceConfig> 
    {
        protected TempTokenServiceConfig sconf = new TempTokenServiceConfig() 
        { 
            TokenLifetime = TimeSpan.FromMilliseconds(100)
        };

        protected override TempTokenServiceConfig config => sconf;

        [Fact]
        public void SimpleSameToken()
        {
            var token = service.GetToken(5);
            var token2 = service.GetToken(5);
            Assert.Equal(token, token2);
        }

        [Fact]
        public void SimpleDifferentToken()
        {
            var token = service.GetToken(5);
            var token2 = service.GetToken(50);
            Assert.NotEqual(token, token2);
        }

        [Fact]
        public void SimpleExpire()
        {
            var token = service.GetToken(5);

            sconf.TokenLifetime = TimeSpan.FromMilliseconds(1);
            System.Threading.Thread.Sleep(5);
            
            var token2 = service.GetToken(5);

            Assert.NotEqual(token, token2);
        }

        [Fact]
        public void ManyTokensDifferent()
        {
            var tokens = new List<string>();

            for(int i = 0; i < 10000; i++)
                tokens.Add(service.GetToken(i));
            
            Assert.True(tokens.Distinct().Count() == tokens.Count);
        }
    }
}