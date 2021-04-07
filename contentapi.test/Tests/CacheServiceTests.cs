using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Implementations;
using Xunit;

namespace contentapi.test
{
    public class CacheServiceTests : ServiceConfigTestBase<CacheService<string, string>, CacheServiceConfig>
    {
        protected override CacheServiceConfig config => new CacheServiceConfig(){ MaxCached = 5, TrimCount = 3 };

        [Fact]
        public void GetEmpty()
        {
            var result = service.GetValues(new List<string>());
            Assert.Empty(result);
        }

        [Fact]
        public void StoreItem()
        {
            service.StoreItem("one", "something");
            var result = service.GetValues(new[]{"one"});
            Assert.Single(result);
            Assert.Equal("something", result.First());
            result = service.GetValues(new[]{"two"});
            Assert.Empty(result);
        }

        [Fact]
        public void GetValues()
        {
            service.StoreItem("one", "something");
            var result = service.GetValues(new[]{"one","two"});
            Assert.Single(result);
            Assert.Equal("something", result.First());
            string myvalue = null;
            Assert.True(service.GetValue("one", ref myvalue));
            Assert.Equal("something", myvalue);
            myvalue = null;
            Assert.False(service.GetValue("two", ref myvalue));
            Assert.Null(myvalue);
        }

        [Fact]
        public void PurgeCache()
        {
            service.StoreItem("one", "something");
            service.StoreItem("two", "somethingelse");
            var result = service.GetValues(new[]{"one", "two"});
            Assert.Equal(2, result.Count);
            service.PurgeCache();
            result = service.GetValues(new[]{"one", "two"});
            Assert.Empty(result);
        }

        [Fact]
        public void OverwriteStore()
        {
            service.StoreItem("one", "something");
            service.StoreItem("one", "somethingelse");
            var result = service.GetValues(new[]{"one"});
            Assert.Single(result);
            Assert.Equal("something", result.First());
            service.StoreItem("one", "crazy!", true);
            result = service.GetValues(new[]{"one"});
            Assert.Single(result);
            Assert.Equal("crazy!", result.First());
        }

        [Fact]
        public void Trim()
        {
            for(var i = 0; i < config.MaxCached; i++)
                service.StoreItem(i.ToString(), $"itsme:{i}");

            var all = service.GetAll();
            Assert.Equal(config.MaxCached, all.Count);

            service.StoreItem("one more", "oops");

            all = service.GetAll();
            Assert.Equal(config.MaxCached - config.TrimCount + 1, all.Count);

            string myvalue = null;
            Assert.True(service.GetValue("one more", ref myvalue));
            Assert.Equal("oops", myvalue);
            var key = (config.MaxCached - 1).ToString();
            Assert.True(service.GetValue(key, ref myvalue));
            Assert.Equal($"itsme:{key}", myvalue);
        }
    }
}