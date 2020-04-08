using System.Linq;
using contentapi.Services.Implementations;
using Xunit;

namespace contentapi.test
{
    public class HashServiceTests : ServiceConfigTestBase<HashService, HashConfig>
    {
        protected override HashConfig config => new HashConfig();

        [Fact]
        public void SimpleGetSalt()
        {
            var bytes = service.GetSalt();
            Assert.True(bytes.Length >= 8);
        }

        [Fact]
        public void SimpleGetHash()
        {
            var salt = service.GetSalt();
            var hash = service.GetHash("text", salt);

            Assert.True(hash.Length > 8);
            Assert.False(salt.SequenceEqual(hash));
        }

        [Fact]
        public void HashIsAFunction()
        {
            var salt = service.GetSalt();
            var hash = service.GetHash("text", salt);
            var hash2 = service.GetHash("text", salt);

            Assert.True(hash.SequenceEqual(hash2));
        }

        [Fact]
        public void SaltModifiesHash()
        {
            var salt = service.GetSalt();
            var hash = service.GetHash("text", salt);

            var salt2 = service.GetSalt();
            var hash2 = service.GetHash("text", salt2);

            Assert.False(salt.SequenceEqual(salt2));
            Assert.False(hash.SequenceEqual(hash2));
        }

        [Fact]
        public void SimpleVerify()
        {
            var salt = service.GetSalt();
            var hash = service.GetHash("MyPassword", salt);
            Assert.True(service.VerifyText("MyPassword", hash, salt));
        }

        [Fact]
        public void SimpleNotVerified()
        {
            var salt = service.GetSalt();
            var hash = service.GetHash("MyPassword", salt);
            Assert.False(service.VerifyText("MyPassword2", hash, salt));
        }

        [Fact]
        public void NotVerifiedSalt()
        {
            var salt = service.GetSalt();
            var hash = service.GetHash("MyPassword", salt);
            Assert.False(service.VerifyText("MyPassword", hash, service.GetSalt()));
        }
    }
}