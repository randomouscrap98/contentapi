using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Configs;
using contentapi.Services.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
    public class BasicChainingTests : ServiceConfigTestBase<ChainService, SystemConfig>
    {
        protected SystemConfig myConfig = new SystemConfig() 
        { 
            ListenTimeout = TimeSpan.FromSeconds(60),
            ListenGracePeriod = TimeSpan.FromSeconds(10)
        };

        protected override SystemConfig config => myConfig;

        protected ChainServices services;

        public BasicChainingTests() : base()
        {
            services = CreateService<ChainServices>();
        }

        [Fact]
        public void BasicSingleTest() //Does the LOW level thing work?
        {
            //Just a SIMPLE little chain!
            var requester = new Requester() { system = true };
            var user = services.user.WriteAsync(new UserViewFull() { username = "simple" }, requester).Result;

            var chain = new ChainRequest<UserSearch, UserViewFull>()
            {
                baseSearch = new UserSearch(),
                retriever = (s) => services.user.SearchAsync(s, requester),
                chains = new List<Chaining>(),
                mergeLock = new object(),
                mergeList = new List<TaggedChainResult>()
            };

            service.ChainAsync(chain, new List<List<IIdView>>()).Wait();
            Assert.Single(chain.mergeList);
            Assert.Equal(user.id, chain.mergeList.First().id);
            Assert.Equal(user.id, ((dynamic)chain.mergeList.First().result).id);
        }
    }
}