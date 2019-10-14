using Xunit;
using contentapi.Services;
using contentapi.Models;
using AutoMapper;
using System;
using System.Linq;

namespace contentapi.test
{
    public class EntityServiceTest : TestBase
    {
        private EntityService CreateService()
        {
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<User,UserView>();
                cfg.CreateMap<UserView,User>();
            }); 
            var mapper = mapperConfig.CreateMapper();
            return new EntityService(mapper, new AccessService());
        }

        [Fact]
        public void SimpleUserSetEntity()
        {
            var service = CreateService();

            var user = new User();
            service.SetNewEntity(user);

            Assert.NotNull(user.Entity);
            Assert.NotNull(user.Entity.AccessList);
            Assert.NotNull(user.Entity.baseAllow);
            Assert.True(user.Entity.createDate >= DateTime.Now.AddSeconds(-60));
        }

        [Fact]
        public void SimpleUserConvert()
        {
            var service = CreateService();

            var user = new UserView()
            {
                username = "Wow",
                role = "SiteAdministrator"
            };

            var newUser = service.ConvertFromView<User, UserView>(user);

            Assert.Equal(user.username, newUser.username);
            Assert.Equal(Role.SiteAdministrator, newUser.role);
        }

        [Fact]
        public void ComplexUserConvert()
        {
            var service = CreateService();

            var user = new UserView()
            {
                username = "Wow",
                role = "SiteAdministrator",
                baseAccess = "CR",
                accessList = new System.Collections.Generic.Dictionary<long, string>()
                {
                    { 5, "UD" },
                    { 6, "U" }
                }
            };

            var newUser = service.ConvertFromView<User, UserView>(user);

            Assert.Equal(user.username, newUser.username);
            Assert.Equal(Role.SiteAdministrator, newUser.role);
            Assert.Equal(EntityAction.Create | EntityAction.Read, newUser.Entity.baseAllow);
            Assert.Equal(EntityAction.Update | EntityAction.Delete, newUser.Entity.AccessList.First(x => x.userId == 5).allow);
            Assert.Equal(EntityAction.Update, newUser.Entity.AccessList.First(x => x.userId == 6).allow);
        }
    }
}