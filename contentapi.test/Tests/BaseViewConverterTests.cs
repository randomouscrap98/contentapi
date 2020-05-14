using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Views.Extensions;
using contentapi.Services.Views.Implementations;
using Xunit;

namespace contentapi.test
{
    public class BaseViewConverterTests : UnitTestBase //ServiceTestBase<BasePermissionViewConverter>
    {
        protected void AssertPermsEqual(Dictionary<string, string> perms1, Dictionary<string, string> perms2)
        {
            Assert.Equal(
                perms1.ToDictionary(x => x.Key, y => y.Value.ToUpper()), 
                perms2.ToDictionary(x => x.Key, y => y.Value.ToUpper()));
        }

        [Fact]
        public void PermissionsTransient()
        {
            var perms = new Dictionary<string, string>()
            {
                { "0", "CR" },
                { "2", "CRUD" },
                { "3", "CRU" }
            };

            var relations = PermissionViewExtensions.ConvertPermsToRelations(perms);
            var reperms = PermissionViewExtensions.ConvertRelationsToPerms(relations);
            AssertPermsEqual(perms, reperms);
        }
    }
}