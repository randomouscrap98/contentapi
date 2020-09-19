
using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Configs;
using contentapi.Services.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
    //public class BanServiceTests : ServiceConfigTestBase<PublicBanViewService, SystemConfig>
    public class BanServiceTests : ServiceConfigTestBase<BanViewService, SystemConfig>
    {
        protected SystemConfig myConfig = new SystemConfig();

        protected override SystemConfig config => myConfig;

        protected BanView GetBasicView()
        {
            return new BanView() { expireDate = DateTime.Now.AddDays(5), message = "haha loser", bannedUserId = 5 };
        }

        [Fact]
        public void ReadNoPerms()
        {
            AssertThrows<AuthorizationException>(() => service.SearchAsync(new BanSearch(), new Requester()).Wait());
        }

        [Fact]
        public void ReadPermsButEmpty()
        {
            myConfig.SuperUsers.Add(1);
            var result = service.SearchAsync(new BanSearch(), new Requester() { userId = 1 }).Result;
            Assert.Empty(result);
        }

        [Fact]
        public void WriteNoPerms()
        {
            AssertThrows<AuthorizationException>(() => service.WriteAsync(GetBasicView(), new Requester()).Wait());
        }

        [Fact]
        public void ReadWrite()
        {
            myConfig.SuperUsers.Add(1);
            var requester = new Requester() { userId = 1 };
            var result = service.WriteAsync(GetBasicView(), requester).Result;
            var bans = service.SearchAsync(new BanSearch(), requester).Result;
            Assert.True(bans.Count == 1);
            Assert.Equal(result, bans.First());
        }

    }
}