using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Xunit;

namespace contentapi.test
{
    public class BaseViewConverterTests : ServiceTestBase<ViewSourceServices>
    {
        protected void AssertPermsEqual(Dictionary<long, string> perms1, Dictionary<long, string> perms2)
        {
            Assert.Equal(
                perms1.ToDictionary(x => x.Key, y => y.Value.ToUpper()), 
                perms2.ToDictionary(x => x.Key, y => y.Value.ToUpper()));
        }

        [Fact]
        public void PermissionsTransient()
        {
            var perms = new Dictionary<long, string>()
            {
                { 0, "CR" },
                { 2, "CRUD" },
                { 3, "CRU" }
            };

            var relations = service.FromPerms(perms);
            var reperms = service.ToPerms(relations);
            AssertPermsEqual(perms, reperms);
        }
    }
}