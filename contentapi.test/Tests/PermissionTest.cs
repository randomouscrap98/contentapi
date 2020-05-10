using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Constants;
using contentapi.Services.Implementations;
using Randomous.EntitySystem;
using Xunit;

namespace contentapi.test
{
    public class PermissionTest : ServiceConfigTestBase<PermissionService, SystemConfig>
    {
        protected SystemConfig myConfig = new SystemConfig()
        {
            SuperUsers = new List<long>() { 5, 10 }
        };

        protected override SystemConfig config => myConfig;

        [Fact]
        public void TestSuperUsersExist()
        {
            //We assume there ARE super users.
            Assert.True(service.SuperUsers.OrderBy(x => x).SequenceEqual(new[] {5L,10L}));
        }

        [Fact]
        public void TestSuperUsersReadonly()
        {
            service.SuperUsers.Remove(5);
            Assert.True(service.SuperUsers.OrderBy(x => x).SequenceEqual(new[] {5L,10L}));
        }

        [Fact]
        public void TestNonUserPermissions()
        {
            var package = NewPackage();

            //A non-user should not be able to do anything
            Assert.False(service.CanUser(1, Keys.CreateAction, package));
            Assert.False(service.CanUser(1, Keys.UpdateAction, package));
            Assert.False(service.CanUser(1, Keys.ReadAction, package));
            Assert.False(service.CanUser(1, Keys.DeleteAction, package));
        }

        [Fact]
        public void TestCreatorPermissions()
        {
            var package = NewPackage();
            package.Relations.Add(new EntityRelation()
            {
                type = Keys.CreatorRelation,
                entityId1 = 1
            });

            //A creator should be able to do everything.
            Assert.True(service.CanUser(1, Keys.CreateAction, package));
            Assert.True(service.CanUser(1, Keys.UpdateAction, package));
            Assert.True(service.CanUser(1, Keys.ReadAction, package));
            Assert.True(service.CanUser(1, Keys.DeleteAction, package));
        }

        [Fact]
        public void TestSuperPermissions()
        {
            var package = NewPackage();

            //BIG WARN: Packages with NO relations will NOT allow super user permissions!
            package.Relations.Add(new EntityRelation()
            {
                type = Keys.CreatorRelation,
                entityId1 = 1
            });

            //A super-user should not be able to read but be able to do everything else
            Assert.True(service.CanUser(5, Keys.CreateAction, package));
            Assert.True(service.CanUser(5, Keys.UpdateAction, package));
            Assert.False(service.CanUser(5, Keys.ReadAction, package));
            Assert.True(service.CanUser(5, Keys.DeleteAction, package));
        }

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

            var relations = service.ConvertPermsToRelations(perms);
            var reperms = service.ConvertRelationsToPerms(relations);
            AssertPermsEqual(perms, reperms);
        }
    }
}